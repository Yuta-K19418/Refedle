using System.Diagnostics;
using System.Text.Json;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Engine.IO.JsonLines;

/// <summary>
/// Schema scanner for JSON Lines format.
/// </summary>
public static class SchemaScanner
{
    /// <summary>
    /// Scans the first N lines of a JSON Lines file and returns an inferred schema.
    /// Seeds the schema from the first valid line, then refines with remaining lines.
    /// </summary>
    /// <param name="rawLines">Raw byte buffers of individual lines.</param>
    /// <param name="initialScanCount">Maximum number of lines to scan (default: 200).</param>
    /// <returns>Result containing the inferred TableSchema, or an error message.</returns>
    public static Result<TableSchema> ScanSchema(
        IReadOnlyList<JsonRawBytes> rawLines,
        int initialScanCount = 200
    )
    {
        if (initialScanCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialScanCount),
                "Initial scan count cannot be negative."
            );
        }

        ArgumentNullException.ThrowIfNull(rawLines);

        if (rawLines.Count == 0)
        {
            return Results.Failure<TableSchema>("No lines provided for schema inference.");
        }

        var linesToProcess = System.Math.Min(rawLines.Count, initialScanCount);

        // Find first valid line to seed the initial schema
        TableSchema? schema = null;
        var startIndex = 0;
        for (var i = 0; i < linesToProcess; i++)
        {
            var seedResult = TrySeedSchema(rawLines[i].Span);
            if (seedResult.IsFailure)
            {
                continue;
            }

            schema = seedResult.Value;
            startIndex = i + 1;
            break;
        }

        if (schema is null)
        {
            return Results.Failure<TableSchema>(
                "No valid JSON objects found in the provided lines."
            );
        }

        // Refine schema with remaining lines
        for (var i = startIndex; i < linesToProcess; i++)
        {
            var refineResult = RefineSchema(schema, rawLines[i].Span);
            if (refineResult.IsFailure)
            {
                continue;
            }

            schema = refineResult.Value;
        }

        return Results.Success(schema);
    }

    /// <summary>
    /// Refines an existing schema with one additional line.
    /// Uses Copy-on-Write: returns the original schema unchanged if no type or nullability changes are detected.
    /// </summary>
    /// <param name="schema">The schema to refine.</param>
    /// <param name="lineBytes">Raw bytes of a single JSON Lines row.</param>
    /// <returns>Result containing the updated TableSchema, or the original schema if the line is malformed.</returns>
    public static Result<TableSchema> RefineSchema(TableSchema schema, ReadOnlySpan<byte> lineBytes)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (lineBytes.IsEmpty)
        {
            return Results.Success(schema);
        }

        var columnMap = new Dictionary<string, ColumnType>(StringComparer.Ordinal);
        var keyOrder = new List<string>();

        foreach (var col in schema.Columns)
        {
            columnMap[col.Name] = col.Type;
            keyOrder.Add(col.Name);
        }

        var observedKeys = new HashSet<string>(StringComparer.Ordinal);

        var scanResult = ScanLine(lineBytes, columnMap, keyOrder, observedKeys);
        if (scanResult.IsFailure)
        {
            return Results.Success(schema);
        }

        var updatedColumns = ComputeUpdatedColumns(schema, keyOrder, columnMap, observedKeys);
        if (updatedColumns.Count == 0)
        {
            return Results.Success(schema);
        }

        var newColumns = RebuildColumns(schema, keyOrder, updatedColumns);
        return Results.Success(schema with { Columns = newColumns });
    }

    // Copy-on-Write: collects only columns whose type or nullability changed relative to schema.
    private static Dictionary<string, ColumnSchema> ComputeUpdatedColumns(
        TableSchema schema,
        List<string> keyOrder,
        Dictionary<string, ColumnType> columnMap,
        HashSet<string> observedKeys)
    {
        var updatedColumns = new Dictionary<string, ColumnSchema>(StringComparer.Ordinal);

        for (var i = 0; i < keyOrder.Count; i++)
        {
            var key = keyOrder[i];
            var type = columnMap[key];

            var originalCol = schema.GetColumn(key);
            if (originalCol is null)
            {
                // New column — always a change
                updatedColumns[key] = new ColumnSchema
                {
                    Name = key,
                    Type = type,
                    IsNullable = true,
                    ColumnIndex = i,
                    DisplayFormat = null,
                };
                continue;
            }

            var updated = originalCol.WithUpdatedType(type);
            if (!observedKeys.Contains(key))
            {
                updated = updated.WithMarkedNullable();
            }

            if (!ReferenceEquals(updated, originalCol))
            {
                updatedColumns[key] = updated;
            }
        }

        return updatedColumns;
    }

    // Rebuilds the full column list in keyOrder, substituting any updated entries.
    private static List<ColumnSchema> RebuildColumns(
        TableSchema schema,
        List<string> keyOrder,
        Dictionary<string, ColumnSchema> updatedColumns)
    {
        var newColumns = new List<ColumnSchema>(keyOrder.Count);
        for (var i = 0; i < keyOrder.Count; i++)
        {
            var key = keyOrder[i];
            if (updatedColumns.TryGetValue(key, out var updatedCol))
            {
                newColumns.Add(updatedCol);
                continue;
            }

            newColumns.Add(
                schema.GetColumn(key)
                    ?? throw new UnreachableException(
                        $"Column '{key}' not found in schema during rebuild."
                    )
            );
        }

        return newColumns;
    }

    /// <summary>
    /// Creates the initial TableSchema from the first valid line.
    /// </summary>
    /// <param name="line">Raw bytes of a single JSON object line.</param>
    /// <returns>Result containing the seeded TableSchema, or an error if the line is invalid.</returns>
    private static Result<TableSchema> TrySeedSchema(ReadOnlySpan<byte> line)
    {
        var columnMap = new Dictionary<string, ColumnType>(StringComparer.Ordinal);
        var keyOrder = new List<string>();
        var observedKeys = new HashSet<string>(StringComparer.Ordinal);

        var scanResult = ScanLine(line, columnMap, keyOrder, observedKeys);
        if (scanResult.IsFailure)
        {
            return Results.Failure<TableSchema>("Invalid JSON line.");
        }

        var columns = new List<ColumnSchema>(keyOrder.Count);
        for (var i = 0; i < keyOrder.Count; i++)
        {
            var key = keyOrder[i];
            columns.Add(
                new ColumnSchema
                {
                    Name = key,
                    Type = columnMap[key],
                    IsNullable = !observedKeys.Contains(key),
                    ColumnIndex = i,
                    DisplayFormat = null,
                }
            );
        }

        return Results.Success(
            new TableSchema { Columns = columns, SourceFormat = DataFormat.JsonLines }
        );
    }

    /// <summary>
    /// Parses one JSON object line and updates the mutable maps in-place.
    /// </summary>
    /// <param name="line">Raw bytes of a single JSON object line.</param>
    /// <param name="columnMap">key → currently inferred ColumnType (mutated in-place).</param>
    /// <param name="keyOrder">Ordered list of keys (first-seen order); new keys are appended.</param>
    /// <param name="observedKeys">Set of keys observed with a non-null value (mutated in-place).</param>
    /// <returns>Success, or Failure if the line is not a valid JSON object.</returns>
    private static Result ScanLine(
        ReadOnlySpan<byte> line,
        Dictionary<string, ColumnType> columnMap,
        List<string> keyOrder,
        HashSet<string> observedKeys
    )
    {
        try
        {
            var reader = new Utf8JsonReader(line);

            if (!reader.Read())
            {
                return Results.Failure("Empty line.");
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return Results.Failure("Line is not a JSON object.");
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return Results.Success();
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var propertyResult = ScanProperty(ref reader, columnMap, keyOrder, observedKeys);
                if (propertyResult.IsFailure)
                {
                    return propertyResult;
                }
            }

            return Results.Success();
        }
        catch (JsonException)
        {
            return Results.Failure("Malformed JSON line.");
        }
    }

    // Reads one "key": value pair (reader positioned at PropertyName) and updates the mutable
    // maps in-place, resolving the column's type against any prior observation of the same key.
    private static Result ScanProperty(
        ref Utf8JsonReader reader,
        Dictionary<string, ColumnType> columnMap,
        List<string> keyOrder,
        HashSet<string> observedKeys)
    {
        var propertyName =
            reader.GetString()
            ?? throw new UnreachableException(
                "GetString() returned null on a PropertyName token."
            );

        if (!reader.Read())
        {
            return Results.Failure("Unexpected end of JSON.");
        }

        if (TypeInferrer.IsNullToken(reader.TokenType))
        {
            // JSON null: do NOT change type, do NOT add to observedKeys
            if (!columnMap.ContainsKey(propertyName))
            {
                columnMap[propertyName] = ColumnType.Text;
                keyOrder.Add(propertyName);
            }

            return Results.Success();
        }

        var inferredType = TypeInferrer.InferType(reader.TokenType, reader.ValueSpan);

        if (
            reader.TokenType == JsonTokenType.StartObject
            || reader.TokenType == JsonTokenType.StartArray
        )
        {
            reader.Skip();
        }

        observedKeys.Add(propertyName);

        if (!columnMap.TryGetValue(propertyName, out var existingType))
        {
            columnMap[propertyName] = inferredType;
            keyOrder.Add(propertyName);
            return Results.Success();
        }

        columnMap[propertyName] = ColumnTypeResolver.Resolve(existingType, inferredType);
        return Results.Success();
    }
}

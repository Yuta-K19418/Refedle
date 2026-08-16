using System.Globalization;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Engine.IO.Csv;

/// <summary>
/// Scans CSV data to infer schema (header + column types).
/// Analyzes a single row to determine column types.
/// </summary>
public static class SchemaScanner
{
    /// <summary>
    /// Scans initial CSV rows (up to 200) and returns the inferred schema for faster stabilization.
    /// </summary>
    /// <param name="columnNames">Column names from header.</param>
    /// <param name="rows">The initial rows to analyze for type inference.</param>
    /// <param name="initialScanCount">Maximum number of rows to scan for initial schema (default: 200).</param>
    /// <returns>Result containing TableSchema or error message.</returns>
    public static Result<TableSchema> ScanSchema(
        IReadOnlyList<string> columnNames,
        IReadOnlyList<CsvDataRow> rows,
        int initialScanCount = 200
    )
    {
        // Validate parameters
        ArgumentNullException.ThrowIfNull(columnNames);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentOutOfRangeException.ThrowIfNegative(initialScanCount);

        if (columnNames.Count == 0)
        {
            return Results.Failure<TableSchema>("CSV has no columns");
        }

        // Header-only CSV: infer Text/nullable schema from column names only
        if (rows.Count == 0)
        {
            var headerOnlyColumns = columnNames.Select((name, idx) => new ColumnSchema
            {
                Name = string.IsNullOrWhiteSpace(name) ? string.Create(CultureInfo.InvariantCulture, $"Column{idx + 1}") : name,
                Type = ColumnType.Text,
                IsNullable = true,
                ColumnIndex = idx,
            }).ToArray();
            return Results.Success(new TableSchema { Columns = headerOnlyColumns, SourceFormat = DataFormat.Csv });
        }

        var actualScanCount = Math.Min(rows.Count, initialScanCount);
        var firstRow = rows[0];

        // Ensure first row has same number of columns as column names
        if (firstRow.Count != columnNames.Count)
        {
            return Results.Failure<TableSchema>(
                $"Row has {firstRow.Count} columns but header has {columnNames.Count} columns"
            );
        }

        // Start with schema from first row
        var initialSchemaResult = InferColumnTypes(columnNames, firstRow);
        var currentSchema = new TableSchema
        {
            Columns = initialSchemaResult,
            SourceFormat = DataFormat.Csv,
        };

        // Refine schema with remaining rows (up to initialScanCount)
        for (var i = 1; i < actualScanCount; i++)
        {
            var row = rows[i];
            if (row.Count != columnNames.Count)
            {
                continue; // Skip malformed rows during initial scan
            }

            var refineResult = RefineSchema(currentSchema, row);
            if (refineResult.IsSuccess)
            {
                currentSchema = refineResult.Value;
            }
        }

        return Results.Success(currentSchema);
    }

    /// <summary>
    /// Infers column types from a single row.
    /// </summary>
    /// <param name="columnNames">Column names.</param>
    /// <param name="row">Single row to analyze.</param>
    /// <returns>List of ColumnSchema with inferred types.</returns>
    private static ColumnSchema[] InferColumnTypes(
        IReadOnlyList<string> columnNames,
        CsvDataRow row
    )
    {
        var columnCount = columnNames.Count;
        var columnSchema = new ColumnSchema[columnCount];

        for (var i = 0; i < columnCount; i++)
        {
            var columnName = columnNames[i];
            var columnValue = row[i];
            var valueSpan = columnValue.Span;

            // Determine nullable status (if value is empty or whitespace-only)
            var isNullable = TypeInferrer.IsEmptyOrWhitespace(valueSpan);

            // Infer type from the value
            var columnType = TypeInferrer.InferType(valueSpan);

            // Generate column name if empty
            var finalColumnName = string.IsNullOrWhiteSpace(columnName)
                ? string.Create(CultureInfo.InvariantCulture, $"Column{i + 1}")
                : columnName;

            columnSchema[i] = new ColumnSchema
            {
                Name = finalColumnName,
                Type = columnType,
                IsNullable = isNullable,
                ColumnIndex = i,
            };
        }

        return columnSchema;
    }

    /// <summary>
    /// Refines an existing schema by analyzing additional rows.
    /// Updates column types and nullable status based on observed values using Copy-on-Write.
    /// </summary>
    /// <param name="schema">The existing schema to refine.</param>
    /// <param name="row">The row to analyze for type refinement.</param>
    /// <returns>Result containing updated TableSchema or error message.</returns>
    public static Result<TableSchema> RefineSchema(TableSchema schema, CsvDataRow row)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(row);

        // Validate row has same number of columns as schema
        if (schema.Columns.Count != row.Count)
        {
            return Results.Failure<TableSchema>(
                $"Row has {row.Count} columns but schema has {schema.Columns.Count} columns"
            );
        }

        var updatedColumns = new Dictionary<int, ColumnSchema>();

        // Process each column
        for (var i = 0; i < schema.ColumnCount; i++)
        {
            var columnSchema = schema.Columns[i];
            var columnValue = row[i];
            var valueSpan = columnValue.Span;

            // Update nullable status for empty or whitespace values
            if (TypeInferrer.IsEmptyOrWhitespace(valueSpan))
            {
                // Use Copy-on-Write pattern with WithMarkedNullable
                updatedColumns[i] = columnSchema.WithMarkedNullable();
                continue;
            }

            // Infer type for this value
            var inferredType = TypeInferrer.InferType(valueSpan);

            // Update column type using Copy-on-Write pattern
            var updatedSchema = columnSchema.WithUpdatedType(inferredType);
            if (!ReferenceEquals(updatedSchema, columnSchema))
            {
                updatedColumns[i] = updatedSchema;
            }
        }

        // If no changes were made, return the original schema
        if (updatedColumns.Count == 0)
        {
            return Results.Success(schema);
        }

        // Create new column schemas with updated values
        var newColumns = new ColumnSchema[schema.Columns.Count];
        for (var i = 0; i < schema.ColumnCount; i++)
        {
            if (updatedColumns.TryGetValue(i, out var updatedColumn))
            {
                newColumns[i] = updatedColumn;
                continue;
            }

            newColumns[i] = schema.Columns[i];
        }

        // Return new TableSchema with updated columns
        var updatedTableSchema = schema with
        {
            Columns = newColumns,
        };
        return Results.Success(updatedTableSchema);
    }
}

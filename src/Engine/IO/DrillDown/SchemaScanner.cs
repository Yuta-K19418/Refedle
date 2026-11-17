using System.Text.Json;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Engine.IO.DrillDown;

/// <summary>
/// Stateless schema-accumulation helpers shared by <see cref="DrillDownSchemaExtractor"/> and the
/// full-aggregation scanner. Holds no state of its own; all accumulator collections are owned and
/// passed in by the caller.
/// </summary>
internal static class SchemaScanner
{
    /// <summary>
    /// Scans a single JSON object's top-level properties, registering new keys in first-seen
    /// order and inferring their column types. Records the observed (non-null) keys into
    /// <paramref name="observedKeys"/> for nullability accounting.
    /// </summary>
    public static void ScanObject(
        ReadOnlySpan<byte> objectBytes,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        HashSet<string> observedKeys)
    {
        Utf8JsonReader reader = new(objectBytes);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return;
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            ScanSingleProperty(ref reader, keyOrder, keySet, columnTypes, observedKeys);
        }
    }

    /// <summary>
    /// Registers <paramref name="key"/> in <paramref name="keyOrder"/> (preserving first-seen
    /// order) when it has not been observed before.
    /// </summary>
    public static void RegisterKeyIfNew(string key, List<string> keyOrder, HashSet<string> keySet)
    {
        if (keySet.Contains(key))
        {
            return;
        }

        keyOrder.Add(key);
        keySet.Add(key);
    }

    /// <summary>
    /// Increments the observation count for each key observed in the current object.
    /// </summary>
    public static void IncrementObservationCounts(
        HashSet<string> observedKeys,
        Dictionary<string, int> keyObservedCount)
    {
        foreach (string key in observedKeys)
        {
            keyObservedCount[key] = keyObservedCount.GetValueOrDefault(key) + 1;
        }
    }

    /// <summary>
    /// Builds the final <see cref="TableSchema"/> from the accumulated key order, types, and
    /// observation counts. A key is marked nullable when it was not observed in every row
    /// (<paramref name="totalRowCount"/>).
    /// </summary>
    public static TableSchema BuildTableSchema(
        List<string> keyOrder,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount,
        int totalRowCount,
        DataFormat format)
    {
        List<ColumnSchema> columns = [];
        for (int i = 0; i < keyOrder.Count; i++)
        {
            string key = keyOrder[i];
            int observedCount = keyObservedCount.GetValueOrDefault(key);
            columns.Add(new ColumnSchema
            {
                Name = key,
                Type = columnTypes.GetValueOrDefault(key, ColumnType.Text),
                IsNullable = observedCount < totalRowCount,
                ColumnIndex = i,
            });
        }

        return new TableSchema { Columns = columns, SourceFormat = format };
    }

    private static void ScanSingleProperty(
        ref Utf8JsonReader reader,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        HashSet<string> observedKeys)
    {
        if (reader.TokenType != JsonTokenType.PropertyName)
        {
            return;
        }

        string key = reader.GetString() ?? string.Empty;

        if (!reader.Read())
        {
            return;
        }

        JsonTokenType tokenType = reader.TokenType;
        ReadOnlySpan<byte> valueSpan = reader.ValueSpan;

        if (tokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            reader.Skip();
        }

        RegisterKeyIfNew(key, keyOrder, keySet);

        if (TypeInferrer.IsNullToken(tokenType))
        {
            return;
        }

        ColumnType inferredType = TypeInferrer.InferType(tokenType, valueSpan);
        observedKeys.Add(key);
        MergeColumnType(key, inferredType, columnTypes);
    }

    private static void MergeColumnType(
        string key,
        ColumnType inferredType,
        Dictionary<string, ColumnType> columnTypes)
    {
        if (!columnTypes.TryGetValue(key, out ColumnType existingType))
        {
            columnTypes[key] = inferredType;
            return;
        }

        columnTypes[key] = ColumnTypeResolver.Resolve(existingType, inferredType);
    }
}

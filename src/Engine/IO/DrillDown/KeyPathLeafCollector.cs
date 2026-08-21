using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Refedle.Engine.IO.Json;
using Refedle.Engine.Types;

namespace Refedle.Engine.IO.DrillDown;

/// <summary>
/// Collects leaf row(s) and schema observations at the end of a KeyPath descent, plus the byte-level
/// value lookup used to descend object-key segments. Extracted from <see cref="KeyPathTraverser"/> so
/// the traversal control flow depends only one-way on collection: KeyPathTraverser calls into this
/// type, never the reverse. All value slicing delegates to <see cref="JsonByteExtractor"/>.
/// </summary>
internal static class KeyPathLeafCollector
{
    internal static void CollectLeafRows(
        JsonRawBytes leafBytes,
        string posHash,
        string colName,
        byte[] colNameUtf8,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount)
    {
        var reader = new Utf8JsonReader(leafBytes.Span);
        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            rows.Add(new FocusedTableRow(leafBytes, posHash));
            var observedKeys = new HashSet<string>(StringComparer.Ordinal);
            SchemaScanner.ScanObject(leafBytes.Span, keyOrder, keySet, columnTypes, observedKeys);
            SchemaScanner.IncrementObservationCounts(observedKeys, keyObservedCount);
            return;
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            CollectArrayLeafRows(leafBytes, posHash, rows, keyOrder, keySet, columnTypes, keyObservedCount);
            return;
        }

        // Primitive leaf (including null) — synthesize a single-key object so
        // JsonObjectCellExtractor can extract it without modification.
        // Note: ScanObject is NOT called here, so no type inference is performed;
        // the synthesized column always receives ColumnType.Text (Phase 2 limitation).
        var synthBytes = SynthesizeObject(colNameUtf8, leafBytes.Span);
        rows.Add(new FocusedTableRow(synthBytes, posHash));
        SchemaScanner.RegisterKeyIfNew(colName, keyOrder, keySet);
        SchemaScanner.IncrementObservationCounts([colName], keyObservedCount);
    }

    internal static void CollectArrayLeafRows(
        JsonRawBytes leafBytes,
        string posHash,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount)
    {
        var reader = new Utf8JsonReader(leafBytes.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            return;
        }

        var elementIndex = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            if (reader.CurrentDepth != 1)
            {
                continue;
            }

            var isObjectElement = reader.TokenType == JsonTokenType.StartObject;
            var elementBytes = JsonByteExtractor.ExtractValueBytes(ref reader, leafBytes);
            var elementHash = string.Create(CultureInfo.InvariantCulture, $"{posHash}:{elementIndex}");

            if (isObjectElement)
            {
                rows.Add(new FocusedTableRow(elementBytes, elementHash));
                var observedKeys = new HashSet<string>(StringComparer.Ordinal);
                SchemaScanner.ScanObject(elementBytes.Span, keyOrder, keySet, columnTypes, observedKeys);
                SchemaScanner.IncrementObservationCounts(observedKeys, keyObservedCount);
                elementIndex++;
                continue;
            }

            // Primitive element (including null) — synthesize {"value": element}.
            var synthBytes = SynthesizeObject("value"u8, elementBytes.Span);
            rows.Add(new FocusedTableRow(synthBytes, elementHash));
            SchemaScanner.RegisterKeyIfNew("value", keyOrder, keySet);
            SchemaScanner.IncrementObservationCounts(["value"], keyObservedCount);
            elementIndex++;
        }
    }

    internal static JsonRawBytes? FindValueByKey(JsonRawBytes objectBytes, string key)
    {
        var reader = new Utf8JsonReader(objectBytes.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            if (!reader.ValueTextEquals(key))
            {
                reader.Skip();
                continue;
            }

            if (!reader.Read())
            {
                return null;
            }

            return JsonByteExtractor.ExtractValueBytes(ref reader, objectBytes);
        }

        return null;
    }

    /// <summary>
    /// Returns the bytes of the array element at <paramref name="index"/>, or <c>null</c> if
    /// <paramref name="arrayBytes"/> is not an array or <paramref name="index"/> is out of range.
    /// </summary>
    internal static JsonRawBytes? FindArrayElementByIndex(JsonRawBytes arrayBytes, int index)
    {
        var reader = new Utf8JsonReader(arrayBytes.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            return null;
        }

        var elementIndex = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return null;
            }

            if (reader.CurrentDepth != 1)
            {
                continue;
            }

            var elementBytes = JsonByteExtractor.ExtractValueBytes(ref reader, arrayBytes);
            if (elementIndex == index)
            {
                return elementBytes;
            }

            elementIndex++;
        }

        return null;
    }

    private static JsonRawBytes SynthesizeObject(ReadOnlySpan<byte> keyUtf8, ReadOnlySpan<byte> valueBytes)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WritePropertyName(keyUtf8);
        writer.WriteRawValue(valueBytes, skipInputValidation: true);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenMemory;
    }
}

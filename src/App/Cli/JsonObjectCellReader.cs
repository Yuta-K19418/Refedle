using System.Text;
using System.Text.Json;
using Refedle.Engine.IO.Json;

namespace Refedle.App.Cli;

/// <summary>
/// Shared typed cell extraction for the CLI batch record readers: decodes a
/// <see cref="CellData"/> from one JSON object's bytes by column name, into a
/// caller-owned <see cref="PooledValueBuffer"/>. See ADR-7.
/// </summary>
internal static class JsonObjectCellReader
{
    /// <summary>
    /// Reads the cell addressed by <paramref name="columnNameUtf8"/> from one JSON object's
    /// bytes. The returned <see cref="CellData.Value"/> span is valid until the next ReadCell
    /// call on the same buffer.
    /// </summary>
    /// <param name="objectBytes">The raw bytes of one complete JSON object.</param>
    /// <param name="columnNameUtf8">The UTF-8 encoded column (property) name to read.</param>
    /// <param name="valueBuffer">The pooled buffer the decoded value is written into.</param>
    /// <returns>The decoded cell, or a non-<see cref="CellPresence.Value"/> presence on null,
    /// missing, or unreadable sources.</returns>
    public static CellData ReadCell(JsonRawBytes objectBytes, ReadOnlySpan<byte> columnNameUtf8, PooledValueBuffer valueBuffer)
    {
        try
        {
            var reader = new Utf8JsonReader(objectBytes.Span);

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return new CellData([], CellPresence.Invalid);
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                if (!reader.ValueTextEquals(columnNameUtf8))
                {
                    reader.Skip();
                    continue;
                }

                if (!reader.Read())
                {
                    return new CellData([], CellPresence.Invalid);
                }

                return ReadPropertyValue(reader, objectBytes, valueBuffer);
            }

            return new CellData([], CellPresence.Missing);
        }
        catch (JsonException)
        {
            return new CellData([], CellPresence.Invalid);
        }
    }

    // Split out to stay under the Sonar cyclomatic-complexity limit (S1541). Passed by value,
    // not by ref, so it owns a copy isolated from the caller's state (ref also fails to
    // compile: CS8168/CS8347); the resulting small, stack-only copy per call is an accepted cost.
    private static CellData ReadPropertyValue(Utf8JsonReader reader, JsonRawBytes containingBytes, PooledValueBuffer valueBuffer)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => new CellData([], CellPresence.Null),
            JsonTokenType.Number => NumberToCellData(reader, valueBuffer),
            JsonTokenType.StartObject or JsonTokenType.StartArray => ObjectOrArrayToCellData(reader, containingBytes, valueBuffer),
            JsonTokenType.String => StringToCellData(reader, valueBuffer),
            JsonTokenType.True => new CellData("true", CellPresence.Value, CellEncoding.Boolean),
            JsonTokenType.False => new CellData("false", CellPresence.Value, CellEncoding.Boolean),
            _ => new CellData([], CellPresence.Invalid),
        };
    }

    // Decoded into the pooled buffer shared across ReadCell calls (valid only until the
    // next call). ValueSpan.Length is a safe char-count upper bound: multi-byte UTF-8 and
    // JSON escapes both use more source bytes than the chars they resolve to.
    private static CellData NumberToCellData(Utf8JsonReader reader, PooledValueBuffer valueBuffer)
    {
        var bytes = reader.ValueSpan;
        var buffer = valueBuffer.Reserve(bytes.Length);
        var charsWritten = Encoding.UTF8.GetChars(bytes, buffer);
        return new CellData(buffer.AsSpan(0, charsWritten), CellPresence.Value, CellEncoding.Raw);
    }

    private static CellData ObjectOrArrayToCellData(Utf8JsonReader reader, JsonRawBytes containingBytes, PooledValueBuffer valueBuffer)
    {
        var bytes = JsonByteExtractor.ExtractValueBytes(ref reader, containingBytes).Span;
        var buffer = valueBuffer.Reserve(bytes.Length);
        var charsWritten = Encoding.UTF8.GetChars(bytes, buffer);
        return new CellData(buffer.AsSpan(0, charsWritten), CellPresence.Value, CellEncoding.Raw);
    }

    private static CellData StringToCellData(Utf8JsonReader reader, PooledValueBuffer valueBuffer)
    {
        var buffer = valueBuffer.Reserve(reader.ValueSpan.Length);
        var charsWritten = reader.CopyString(buffer);
        return new CellData(buffer.AsSpan(0, charsWritten), CellPresence.Value, CellEncoding.PlainText);
    }
}

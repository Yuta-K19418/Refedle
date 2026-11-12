using System.Text;
using System.Text.Json;
using Refedle.Engine.Utilities;

namespace Refedle.Engine.IO.Json;

/// <summary>
/// Extracts cell values from a raw JSON Object value by property (column) name.
/// Uses Utf8JsonReader for zero-allocation scanning of top-level properties.
/// Shared by JSON Lines table sources and the DrillDown FocusedTable source.
/// </summary>
public static class JsonObjectCellExtractor
{
    /// <summary>
    /// Cell value returned for malformed JSON.
    /// </summary>
    public const string ErrorValue = "<error>";

    /// <summary>
    /// Cell value returned for missing keys or JSON null.
    /// </summary>
    public const string NullValue = "<null>";

    /// <summary>
    /// Extracts a cell value from a raw JSON line by column name.
    /// </summary>
    /// <param name="lineBytes">Raw bytes of a single JSON Lines row.</param>
    /// <param name="columnNameUtf8">Pre-encoded UTF-8 bytes of the target column name.</param>
    /// <returns>
    /// String representation of the cell value.
    /// Returns "&lt;null&gt;" for missing keys or JSON null.
    /// Returns "&lt;error&gt;" for malformed JSON.
    /// </returns>
    public static string ExtractCell(ReadOnlySpan<byte> lineBytes, ReadOnlySpan<byte> columnNameUtf8)
    {
        if (lineBytes.IsEmpty)
        {
            return ErrorValue;
        }

        try
        {
            var reader = new Utf8JsonReader(lineBytes);

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return ErrorValue;
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
                    return ErrorValue;
                }

                return FormatValue(ref reader);
            }

            return NullValue;
        }
        catch (JsonException)
        {
            return ErrorValue;
        }
    }

    private static string FormatValue(ref Utf8JsonReader reader)
    {
        // Numbers return the raw token text (e.g. "1.50", "1e3") so the original
        // notation is preserved; cast columns are reformatted by CellDisplayFormatter.
        return reader.TokenType switch
        {
            JsonTokenType.Number => Encoding.UTF8.GetString(reader.ValueSpan),
            JsonTokenType.String => StringUtility.DecodeJsonString(ref reader),
            JsonTokenType.True => "True",
            JsonTokenType.False => "False",
            JsonTokenType.Null => NullValue,
            JsonTokenType.StartObject => JsonByteExtractor.FormatObjectPreview(ref reader),
            JsonTokenType.StartArray => JsonByteExtractor.FormatArrayPreview(ref reader),
            _ => NullValue,
        };
    }
}

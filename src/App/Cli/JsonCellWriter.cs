using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Refedle.Engine;

namespace Refedle.App.Cli;

/// <summary>
/// Encodes a single <see cref="CellData"/> as one JSON property on an open object, shared by
/// <see cref="JsonLinesRecordWriter"/> and <see cref="JsonArrayRecordWriter"/>. A pure function:
/// the caller owns the <see cref="Utf8JsonWriter"/> and its lifecycle.
/// </summary>
internal static class JsonCellWriter
{
    public static void WriteCellData(
        Utf8JsonWriter writer, BatchOutputSchema outputSchema, int outputColumnIndex, CellData cell)
    {
        if (cell.Presence == CellPresence.Missing)
        {
            return;
        }

        writer.WritePropertyName(outputSchema.Columns[outputColumnIndex].OutputName);

        if (cell.Presence == CellPresence.Null)
        {
            writer.WriteNullValue();
            return;
        }

        if (cell.Presence == CellPresence.Invalid)
        {
            writer.WriteStringValue(string.Empty);
            return;
        }

        if (cell.Encoding == CellEncoding.Raw)
        {
            writer.WriteRawValue(cell.Value, skipInputValidation: true);
            return;
        }

        if (cell.Encoding == CellEncoding.Numeric)
        {
            writeNumericValue(writer, cell.Value);
            return;
        }

        if (cell.Encoding == CellEncoding.Boolean)
        {
            writer.WriteBooleanValue(bool.Parse(cell.Value));
            return;
        }

        if (cell.Encoding == CellEncoding.PlainText)
        {
            writer.WriteStringValue(cell.Value);
            return;
        }

        throw new UnreachableException($"Unhandled CellEncoding: {cell.Encoding}");

        static void writeNumericValue(Utf8JsonWriter writer, ReadOnlySpan<char> value)
        {
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            {
                writer.WriteNumberValue(longValue);
                return;
            }

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue))
            {
                writer.WriteNumberValue(doubleValue);
                return;
            }

            throw new UnreachableException("CellEncoding.Numeric guarantees the value re-parses as long or double.");
        }
    }
}

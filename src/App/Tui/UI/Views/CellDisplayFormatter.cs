using System.Globalization;
using Refedle.Engine.Types;

namespace Refedle.App.Tui.UI.Views;

/// <summary>
/// Shared cell formatting for the lazy table transformers. Formats a raw cell string
/// according to the target column type; callers gate on
/// <see cref="LazyTransformerBase.ShouldFormat"/> so untouched columns display raw values.
/// </summary>
internal static class CellDisplayFormatter
{
    private const string ParseFailureLabel = "<invalid>";

    /// <summary>
    /// The format applied to Timestamp columns that were cast (or formatted with an empty
    /// target format) but never given an explicit format string.
    /// </summary>
    internal const string DefaultTimestampFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// Formats a raw cell string value according to the target column type.
    /// Returns the raw value for <see cref="ColumnType.Text"/>, <see cref="ColumnType.JsonObject"/>,
    /// and <see cref="ColumnType.JsonArray"/>. Returns <c>"&lt;invalid&gt;"</c> if parsing fails.
    /// </summary>
    internal static string FormatCellValue(string rawValue, ColumnType targetType, string? formatString) =>
        targetType switch
        {
            ColumnType.WholeNumber => FormatWholeNumber(rawValue),
            ColumnType.FloatingPoint => FormatFloatingPoint(rawValue),
            ColumnType.Boolean => FormatBoolean(rawValue),
            ColumnType.Timestamp => FormatTimestamp(rawValue, formatString),
            _ => rawValue,
        };

    private static string FormatWholeNumber(string rawValue) =>
        long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)
            ? l.ToString(CultureInfo.InvariantCulture)
            : ParseFailureLabel;

    private static string FormatFloatingPoint(string rawValue) =>
        double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d.ToString(CultureInfo.InvariantCulture)
            : ParseFailureLabel;

    private static string FormatBoolean(string rawValue)
    {
        if (!bool.TryParse(rawValue, out var b))
        {
            return ParseFailureLabel;
        }

        return b ? "true" : "false";
    }

    private static string FormatTimestamp(string rawValue, string? formatString)
    {
        if (!DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return ParseFailureLabel;
        }

        var format = string.IsNullOrEmpty(formatString) ? DefaultTimestampFormat : formatString;
        return dt.ToString(format, CultureInfo.InvariantCulture);
    }
}

using System.Globalization;
using Refedle.Engine.Types;

namespace Refedle.Engine.IO.Csv;

/// <summary>
/// Infers column type from char data.
/// Designed to work with CsvDataRow (IReadOnlyList&lt;ReadOnlyMemory&lt;char&gt;&gt;).
/// </summary>
public static class TypeInferrer
{
    /// <summary>
    /// Infers the most specific type for a char span.
    /// Type priority: Boolean > WholeNumber > FloatingPoint > Timestamp > Text.
    /// </summary>
    /// <param name="value">The char span to analyze.</param>
    /// <returns>The inferred ColumnType.</returns>
    public static ColumnType InferType(ReadOnlySpan<char> value)
    {
        // Try parsing in priority order (most specific first)
        if (TryParseBoolean(value, out _))
        {
            return ColumnType.Boolean;
        }

        if (TryParseWholeNumber(value, out _))
        {
            return ColumnType.WholeNumber;
        }

        if (TryParseFloatingPoint(value, out _))
        {
            return ColumnType.FloatingPoint;
        }

        if (TryParseTimestamp(value, out _))
        {
            return ColumnType.Timestamp;
        }

        // Fallback to Text (including empty values)
        return ColumnType.Text;
    }

    /// <summary>
    /// Tries to parse value as Boolean (case-insensitive true/false).
    /// </summary>
    /// <param name="value">The char span to parse.</param>
    /// <param name="result">The parsed boolean value if successful.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParseBoolean(ReadOnlySpan<char> value, out bool result)
    {
        var trimmed = value.Trim();
        return bool.TryParse(trimmed, out result);
    }

    /// <summary>
    /// Tries to parse value as WholeNumber (int64).
    /// </summary>
    /// <param name="value">The char span to parse.</param>
    /// <param name="result">The parsed long value if successful.</param>
    /// <returns>True if parsing succeeded and consumed entire value, false otherwise.</returns>
    public static bool TryParseWholeNumber(ReadOnlySpan<char> value, out long result)
    {
        var trimmed = value.Trim();
        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Tries to parse value as FloatingPoint (double).
    /// </summary>
    /// <param name="value">The char span to parse.</param>
    /// <param name="result">The parsed double value if successful.</param>
    /// <returns>True if parsing succeeded and consumed entire value, false otherwise.</returns>
    public static bool TryParseFloatingPoint(ReadOnlySpan<char> value, out double result)
    {
        var trimmed = value.Trim();
        return double.TryParse(
            trimmed,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out result
        );
    }

    /// <summary>
    /// Tries to parse value as Timestamp (DateTime).
    /// Supports ISO 8601 and common date formats.
    /// </summary>
    /// <param name="value">The char span to parse.</param>
    /// <param name="result">The parsed DateTime value if successful.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParseTimestamp(ReadOnlySpan<char> value, out DateTime result)
    {
        var trimmed = value.Trim();
        return DateTime.TryParse(
            trimmed,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result
        );
    }

    /// <summary>
    /// Checks if the char span is empty or contains only whitespace.
    /// </summary>
    /// <param name="value">The char span to check.</param>
    /// <returns>True if empty or whitespace-only, false otherwise.</returns>
    public static bool IsEmptyOrWhitespace(ReadOnlySpan<char> value)
    {
        return value.IsEmpty || value.IsWhiteSpace();
    }
}

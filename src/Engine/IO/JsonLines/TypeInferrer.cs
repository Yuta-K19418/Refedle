using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Refedle.Engine.Types;

namespace Refedle.Engine.IO.JsonLines;

/// <summary>
/// Infers column type from JSON token data.
/// Designed to work with Utf8JsonReader in the JSON Lines pipeline.
/// </summary>
/// <remarks>
/// Methods accept <see cref="JsonTokenType"/> and <see cref="ReadOnlySpan{T}"/> directly
/// rather than <c>ref Utf8JsonReader</c>, making it explicit that no reader state is mutated.
/// </remarks>
public static class TypeInferrer
{
    /// <summary>
    /// Reads the token type and raw value span and returns the inferred ColumnType.
    /// Caller must ensure the token is NOT <see cref="JsonTokenType.Null"/> before calling
    /// (use <see cref="IsNullToken"/> first).
    /// Returns <see cref="ColumnType.Text"/> as fallback for any unexpected tokens.
    /// </summary>
    /// <param name="tokenType">The current JSON token type.</param>
    /// <param name="valueSpan">
    /// The raw UTF-8 bytes of the current token value (i.e. <c>reader.ValueSpan</c>).
    /// Used when <paramref name="tokenType"/> is <see cref="JsonTokenType.Number"/> or
    /// <see cref="JsonTokenType.String"/>.
    /// </param>
    public static ColumnType InferType(JsonTokenType tokenType, ReadOnlySpan<byte> valueSpan) =>
        tokenType switch
        {
            JsonTokenType.Number => InferNumberType(valueSpan),
            JsonTokenType.String => InferStringType(valueSpan),
            JsonTokenType.True or JsonTokenType.False => ColumnType.Boolean,
            JsonTokenType.StartObject => ColumnType.JsonObject,
            JsonTokenType.StartArray => ColumnType.JsonArray,
            _ => ColumnType.Text,
        };

    private static ColumnType InferNumberType(ReadOnlySpan<byte> valueSpan)
    {
        // Utf8Parser.TryParse operates directly on the UTF-8 byte span,
        // avoiding string allocation. It is equivalent to what
        // Utf8JsonReader.TryGetInt64 does internally, but does not require
        // passing ref Utf8JsonReader and makes the absence of side effects explicit.
        // bytesConsumed == valueSpan.Length guards against partial matches
        // (e.g. "123abc"), which cannot occur for valid JSON tokens but is
        // included for correctness.
        if (
            Utf8Parser.TryParse(valueSpan, out long _, out var bytesConsumed)
            && bytesConsumed == valueSpan.Length
        )
        {
            return ColumnType.WholeNumber;
        }

        // Decimal point present (invariant: '.' only) → FloatingPoint.
        // Numbers without '.' that overflow int64 (e.g. big integers) → Text.
        return valueSpan.Contains((byte)'.') ? ColumnType.FloatingPoint : ColumnType.Text;
    }

    private static ColumnType InferStringType(ReadOnlySpan<byte> valueSpan) =>
        TryParseTimestamp(valueSpan, out _) ? ColumnType.Timestamp : ColumnType.Text;

    /// <summary>
    /// Returns true if the token type is <see cref="JsonTokenType.Null"/>.
    /// Mirrors the role of <c>TypeInferrer.IsEmptyOrWhitespace()</c> in the CSV pipeline.
    /// </summary>
    /// <param name="tokenType">The current JSON token type.</param>
    public static bool IsNullToken(JsonTokenType tokenType) =>
        tokenType == JsonTokenType.Null;

    /// <summary>
    /// Tries to parse UTF-8 bytes as Timestamp (DateTime).
    /// Supports ISO 8601 and common date formats.
    /// </summary>
    /// <param name="valueSpan">The UTF-8 byte span to parse.</param>
    /// <param name="result">The parsed DateTime value if successful.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParseTimestamp(ReadOnlySpan<byte> valueSpan, out DateTime result)
    {
        if (valueSpan.IsEmpty)
        {
            result = default;
            return false;
        }

        var charCount = Encoding.UTF8.GetCharCount(valueSpan);
        var buffer = ArrayPool<char>.Shared.Rent(charCount);
        try
        {
            var charSpan = buffer.AsSpan(0, Encoding.UTF8.GetChars(valueSpan, buffer));
            return DateTime.TryParse(
                charSpan.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result
            );
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}

using System.Globalization;
using System.Text.Json;

namespace Refedle.Engine.IO.Json;

/// <summary>
/// Shared Engine-layer utility for JSON traversal primitives: extracting the raw bytes of a
/// nested value, counting an Object's top-level properties or an Array's top-level elements,
/// and formatting collapsed preview text for both. Extracted to a common location so the
/// App-layer tree node helper can reuse it without duplicating the depth-tracking logic.
/// </summary>
public static class JsonByteExtractor
{
    /// <summary>
    /// Advances <paramref name="reader"/> past the current nested value (Object or Array) and
    /// returns a slice of <paramref name="rawJson"/> covering it exactly.
    /// The reader must be positioned at a <see cref="JsonTokenType.StartObject"/> or
    /// <see cref="JsonTokenType.StartArray"/> token.
    /// </summary>
    /// <param name="reader">The JSON reader positioned at a StartObject or StartArray token.</param>
    /// <param name="rawJson">The full raw JSON bytes containing the nested structure.</param>
    /// <returns>A slice of the raw bytes covering exactly the nested structure.</returns>
    public static JsonRawBytes ExtractNestedBytes(
        ref Utf8JsonReader reader,
        JsonRawBytes rawJson)
    {
        int startPosition = (int)reader.TokenStartIndex;
        int depth = 1;

        while (depth > 0 && reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                depth++;
            }

            if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                depth--;
            }
        }

        int endPosition = (int)reader.TokenStartIndex + 1;
        return rawJson.Slice(startPosition, endPosition - startPosition);
    }

    /// <summary>
    /// Returns the raw bytes of the value at the reader's current token. Nested structures
    /// (Object/Array) are read through <see cref="ExtractNestedBytes"/>; primitives are sliced
    /// directly from <paramref name="containingBytes"/> between the token start and consumed offset.
    /// </summary>
    public static JsonRawBytes ExtractValueBytes(ref Utf8JsonReader reader, JsonRawBytes containingBytes)
    {
        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            return ExtractNestedBytes(ref reader, containingBytes);
        }

        int start = (int)reader.TokenStartIndex;
        int end = (int)reader.BytesConsumed;
        return containingBytes.Slice(start, end - start);
    }

    /// <summary>
    /// Counts the top-level properties of a JSON object by tracking brace/bracket depth.
    /// The reader must be positioned at a <see cref="JsonTokenType.StartObject"/> token; on return
    /// the reader has consumed the entire object, ending at its matching EndObject token.
    /// </summary>
    public static int CountObjectProperties(ref Utf8JsonReader reader)
    {
        int propertyCount = 0;
        int depth = 1;

        while (depth > 0 && reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                depth++;
                continue;
            }

            if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                depth--;
            }

            if (depth == 1 && reader.TokenType == JsonTokenType.PropertyName)
            {
                propertyCount++;
            }
        }

        return propertyCount;
    }

    /// <summary>
    /// Counts the top-level elements of a JSON array by tracking bracket/brace depth.
    /// The reader must be positioned at a <see cref="JsonTokenType.StartArray"/> token; on return
    /// the reader has consumed the entire array, ending at its matching EndArray token.
    /// </summary>
    public static int CountArrayElements(ref Utf8JsonReader reader)
    {
        int elementCount = 0;
        int depth = 1;

        while (depth > 0 && reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                depth++;
                continue;
            }

            if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                depth--;
            }

            if (depth == 1)
            {
                elementCount++;
            }
        }

        return elementCount;
    }

    /// <summary>
    /// Formats a JSON object's collapsed preview text (e.g. "{Object: 3 properties}"). The reader
    /// must be positioned at a <see cref="JsonTokenType.StartObject"/> token.
    /// </summary>
    public static string FormatObjectPreview(ref Utf8JsonReader reader)
    {
        int propertyCount = CountObjectProperties(ref reader);
        return string.Create(CultureInfo.InvariantCulture, $"{{Object: {propertyCount:N0} properties}}");
    }

    /// <summary>
    /// Formats a JSON array's collapsed preview text (e.g. "[Array: 3 items]"). The reader must be
    /// positioned at a <see cref="JsonTokenType.StartArray"/> token.
    /// </summary>
    public static string FormatArrayPreview(ref Utf8JsonReader reader)
    {
        int elementCount = CountArrayElements(ref reader);
        return string.Create(CultureInfo.InvariantCulture, $"[Array: {elementCount:N0} items]");
    }
}

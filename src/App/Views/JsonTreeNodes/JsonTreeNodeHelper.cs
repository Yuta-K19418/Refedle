using System.Globalization;
using System.Text.Json;
using Refedle.Engine.IO.Json;
using Terminal.Gui.Views;

namespace Refedle.App.Views.JsonTreeNodes;

/// <summary>
/// Shared utilities for JSON tree node parsing and display.
/// </summary>
internal static class JsonTreeNodeHelper
{
    /// <summary>
    /// Creates a child tree node from the current JSON token.
    /// </summary>
    /// <param name="reader">The JSON reader positioned at a value token.</param>
    /// <param name="label">The display label prefix (e.g. "name" or "[0]").</param>
    /// <param name="rawJson">The raw JSON bytes for extracting nested structures.</param>
    /// <param name="recordPosition">Ancestor root record position to propagate to child nodes.</param>
    /// <param name="parentNode">The parent node, used to build a KeyPath via upward traversal.</param>
    /// <returns>A tree node representing the current token, or null if unrecognized.</returns>
    internal static ITreeNode? CreateChildNode(
        ref Utf8JsonReader reader,
        string label,
        JsonRawBytes rawJson,
        long? recordPosition = null,
        ITreeNode? parentNode = null
    )
    {
        return reader.TokenType switch
        {
            JsonTokenType.StartObject => CreateNestedObjectNode(ref reader, label, rawJson, recordPosition, parentNode),
            JsonTokenType.StartArray => CreateNestedArrayNode(ref reader, label, rawJson, recordPosition, parentNode),
            JsonTokenType.String => new JsonValueTreeNode(
                $"{label}: \"{EscapeString(reader.GetString() ?? string.Empty)}\""
            )
            {
                ValueKind = JsonValueKind.String,
                KeyName = label,
                ParentNode = parentNode,
            },
            JsonTokenType.Number => CreateNumberNode(ref reader, label, parentNode),
            JsonTokenType.True => new JsonValueTreeNode($"{label}: true")
            {
                ValueKind = JsonValueKind.True,
                KeyName = label,
                ParentNode = parentNode,
            },
            JsonTokenType.False => new JsonValueTreeNode($"{label}: false")
            {
                ValueKind = JsonValueKind.False,
                KeyName = label,
                ParentNode = parentNode,
            },
            JsonTokenType.Null => new JsonValueTreeNode($"{label}: <null>")
            {
                ValueKind = JsonValueKind.Null,
                KeyName = label,
                ParentNode = parentNode,
            },
            _ => new JsonValueTreeNode($"{label}: <unknown>")
            {
                ValueKind = JsonValueKind.Undefined,
                KeyName = label,
                ParentNode = parentNode,
            },
        };
    }

    /// <summary>
    /// Escapes special characters in a string for tree node display.
    /// </summary>
    internal static string EscapeString(string value)
    {
        return value
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static JsonObjectTreeNode CreateNestedObjectNode(
        ref Utf8JsonReader reader,
        string label,
        JsonRawBytes rawJson,
        long? recordPosition,
        ITreeNode? parentNode
    )
    {
        var objectBytes = JsonByteExtractor.ExtractNestedBytes(ref reader, rawJson);
        return new JsonObjectTreeNode(objectBytes, $"{label}: ")
        {
            KeyName = label,
            RecordPosition = recordPosition,
            ParentNode = parentNode,
        };
    }

    private static JsonArrayTreeNode CreateNestedArrayNode(
        ref Utf8JsonReader reader,
        string label,
        JsonRawBytes rawJson,
        long? recordPosition,
        ITreeNode? parentNode
    )
    {
        var arrayBytes = JsonByteExtractor.ExtractNestedBytes(ref reader, rawJson);
        return new JsonArrayTreeNode(arrayBytes, $"{label}: ")
        {
            KeyName = label,
            RecordPosition = recordPosition,
            ParentNode = parentNode,
        };
    }

    private static JsonValueTreeNode CreateNumberNode(ref Utf8JsonReader reader, string label, ITreeNode? parentNode)
    {
        if (reader.TryGetDecimal(out var decimalValue))
        {
            return new JsonValueTreeNode(string.Create(CultureInfo.InvariantCulture, $"{label}: {decimalValue}"))
            {
                ValueKind = JsonValueKind.Number,
                KeyName = label,
                ParentNode = parentNode,
            };
        }

        var doubleValue = reader.GetDouble();
        return new JsonValueTreeNode(string.Create(CultureInfo.InvariantCulture, $"{label}: {doubleValue}"))
        {
            ValueKind = JsonValueKind.Number,
            KeyName = label,
            ParentNode = parentNode,
        };
    }
}

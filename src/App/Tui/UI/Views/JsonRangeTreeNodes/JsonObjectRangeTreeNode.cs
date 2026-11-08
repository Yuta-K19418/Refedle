using System.Globalization;
using System.Text.Json;
using Refedle.App.Tui.UI.Views.JsonTreeNodes;
using Refedle.Engine.IO.JsonObject;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI.Views.JsonRangeTreeNodes;

/// <summary>
/// Represents a range within a large JSON Object's top-level keys.
/// When <c>Count &gt; <see cref="RangePartitionPolicy.RangeSize"/></c>, children are nested
/// <see cref="JsonObjectRangeTreeNode"/> instances; otherwise, children are individual key nodes.
/// Children are loaded on demand via <see cref="RangeTreeNodeBase.EnsureChildrenLoaded"/>.
/// </summary>
internal sealed class JsonObjectRangeTreeNode : RangeTreeNodeBase
{
    private readonly IReadOnlyList<JsonObjectEntry> _entries;

    internal JsonObjectRangeTreeNode(IReadOnlyList<JsonObjectEntry> entries, long startIndex, long count)
        : base(startIndex, count)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries;
        Text = count == 0
            ? string.Create(CultureInfo.InvariantCulture, $"[{startIndex:N0} - (empty)]")
            : string.Create(CultureInfo.InvariantCulture, $"[{startIndex:N0} - {startIndex + count - 1:N0}]");
    }

    /// <summary>
    /// Creates a key node for display from a top-level entry's raw JSON value bytes.
    /// Mirrors <see cref="JsonArrayRangeTreeNode.CreateElementNode"/>:
    /// reads the first token from <paramref name="valueBytes"/> and dispatches to the
    /// appropriate node type (<see cref="JsonObjectTreeNode"/>,
    /// <see cref="JsonArrayTreeNode"/>, or <see cref="JsonValueTreeNode"/>),
    /// then prepends <c>"{key}: "</c> to the node's display text.
    /// </summary>
    /// <param name="key">The top-level property key.</param>
    /// <param name="valueBytes">The raw JSON bytes of the property value.</param>
    /// <returns>A tree node representing the key-value pair.</returns>
    internal static ITreeNode CreateKeyNode(string key, JsonRawBytes valueBytes)
    {
        var prefix = $"{key}: ";
        ITreeNode invalidNode() =>
            new JsonValueTreeNode($"{prefix}[Invalid JSON]") { ValueKind = JsonValueKind.Undefined };

        if (valueBytes.IsEmpty)
        {
            return invalidNode();
        }

        var reader = new Utf8JsonReader(valueBytes.Span);

        try
        {
            // With a final block and default options, the first Read() either produces a token
            // or throws if no token can be read, so it cannot return false.
            reader.Read();

            return reader.TokenType switch
            {
                JsonTokenType.StartObject => new JsonObjectTreeNode(valueBytes, prefix) { KeyName = key },
                JsonTokenType.StartArray => new JsonArrayTreeNode(valueBytes, prefix) { KeyName = key },
                _ => new JsonValueTreeNode($"{prefix}{reader.GetPrimitiveDisplay()}")
                {
                    ValueKind = reader.TokenType.ToJsonValueKind(),
                },
            };
        }
        catch (JsonException)
        {
            return invalidNode();
        }
    }

    /// <inheritdoc/>
    protected override RangeTreeNodeBase CreateRangeNode(long startIndex, long count) =>
        new JsonObjectRangeTreeNode(_entries, startIndex, count);

    /// <inheritdoc/>
    /// <remarks>
    /// Creates individual key nodes from the in-memory entries for the current range.
    /// </remarks>
    protected override void AddDirectChildren()
    {
        List<ITreeNode> children = [];

        for (var i = 0L; i < Count; i++)
        {
            var entry = _entries[(int)(StartIndex + i)];
            children.Add(CreateKeyNode(entry.Key, entry.Value));
        }

        Children = children;
    }
}

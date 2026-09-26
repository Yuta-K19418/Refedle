using System.Globalization;
using System.Text.Json;
using Refedle.App.Tui.Ui.Views.JsonTreeNodes;
using Refedle.Engine.IO;
using Refedle.Engine.IO.JsonArray;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views.JsonRangeTreeNodes;

/// <summary>
/// Represents a range within a large JSON Array.
/// When <c>Count &gt; <see cref="RangePartitionPolicy.RangeSize"/></c>, children are nested
/// <see cref="JsonArrayRangeTreeNode"/> instances; otherwise, children are individual element nodes.
/// Children are loaded on demand via <see cref="RangeTreeNodeBase.EnsureChildrenLoaded"/>.
/// </summary>
internal sealed class JsonArrayRangeTreeNode : RangeTreeNodeBase
{
    private readonly IRowIndexer _indexer;
    private readonly ElementReader _reader;

    internal JsonArrayRangeTreeNode(IRowIndexer indexer, ElementReader reader, long startIndex, long count)
        : base(startIndex, count)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(reader);
        _indexer = indexer;
        _reader = reader;
        Text = count == 0
            ? string.Create(CultureInfo.InvariantCulture, $"[{startIndex:N0} - (empty)]")
            : string.Create(CultureInfo.InvariantCulture, $"[{startIndex:N0} - {startIndex + count - 1:N0}]");
    }

    /// <summary>
    /// Creates an element node for display from raw JSON bytes.
    /// </summary>
    internal static ITreeNode CreateElementNode(JsonRawBytes bytes, long index)
    {
        var prefix = string.Create(CultureInfo.InvariantCulture, $"[{index:N0}]: ");
        ITreeNode invalidNode() =>
            new JsonValueTreeNode($"{prefix}[Invalid JSON]") { ValueKind = JsonValueKind.Undefined };

        if (bytes.IsEmpty)
        {
            return invalidNode();
        }

        var reader = new Utf8JsonReader(bytes.Span);

        try
        {
            if (!reader.Read())
            {
                return invalidNode();
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                return new JsonObjectTreeNode(bytes, prefix) { RecordPosition = index };
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                return new JsonArrayTreeNode(bytes, prefix) { RecordPosition = index };
            }

            return new JsonValueTreeNode($"{prefix}{reader.GetPrimitiveDisplay()}")
            {
                ValueKind = reader.TokenType.ToJsonValueKind(),
            };
        }
        catch (JsonException)
        {
            return invalidNode();
        }
    }

    /// <inheritdoc/>
    protected override RangeTreeNodeBase CreateRangeNode(long startIndex, long count) =>
        new JsonArrayRangeTreeNode(_indexer, _reader, startIndex, count);

    /// <inheritdoc/>
    /// <remarks>
    /// Reads element bytes and creates individual element nodes for the current range.
    /// </remarks>
    protected override void AddDirectChildren()
    {
        var (byteOffset, rowOffset) = _indexer.GetCheckPoint(StartIndex);
        var elements = _reader.ReadElements(byteOffset, rowOffset, (int)Count);
        List<ITreeNode> children = [];

        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i].IsEmpty)
            {
                continue;
            }

            children.Add(CreateElementNode(elements[i], StartIndex + i));
        }

        Children = children;
    }
}

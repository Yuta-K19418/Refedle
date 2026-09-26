using System.Globalization;
using System.Text.Json;
using Refedle.App.Tui.Ui.Views.JsonTreeNodes;
using Refedle.Engine.IO;
using Refedle.Engine.IO.JsonLines;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views.JsonRangeTreeNodes;

/// <summary>
/// Represents a range within a large JSON Lines file.
/// When <c>Count &gt; <see cref="RangePartitionPolicy.RangeSize"/></c>, children are nested
/// <see cref="JsonLinesRangeTreeNode"/> instances; otherwise, children are individual line nodes.
/// Children are loaded on demand via <see cref="RangeTreeNodeBase.EnsureChildrenLoaded"/>.
/// </summary>
internal sealed class JsonLinesRangeTreeNode : RangeTreeNodeBase
{
    private readonly IRowIndexer _indexer;
    private readonly RowReader _reader;

    internal JsonLinesRangeTreeNode(IRowIndexer indexer, RowReader reader, long startIndex, long count)
        : base(startIndex, count)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(reader);
        _indexer = indexer;
        _reader = reader;
        Text = count == 0
            ? string.Create(CultureInfo.InvariantCulture, $"Lines {startIndex + 1:N0} (empty)")
            : string.Create(CultureInfo.InvariantCulture, $"Lines {startIndex + 1:N0} - {startIndex + count:N0}");
    }

    /// <summary>
    /// Creates a line node for display from raw JSON bytes.
    /// <paramref name="lineIndex"/> is 0-based; the display label uses <c>lineNumber = lineIndex + 1</c> (1-based).
    /// </summary>
    internal static ITreeNode CreateLineNode(JsonRawBytes lineBytes, long lineIndex)
    {
        var prefix = string.Create(CultureInfo.InvariantCulture, $"Line {lineIndex + 1:N0}: ");
        ITreeNode invalidNode() =>
            new JsonValueTreeNode($"{prefix}[Invalid JSON]") { ValueKind = JsonValueKind.Undefined };

        if (lineBytes.IsEmpty)
        {
            return invalidNode();
        }

        var reader = new Utf8JsonReader(lineBytes.Span);

        try
        {
            if (!reader.Read())
            {
                return invalidNode();
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                return new JsonObjectTreeNode(lineBytes, prefix) { RecordPosition = lineIndex + 1 };
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                return new JsonArrayTreeNode(lineBytes, prefix) { RecordPosition = lineIndex + 1 };
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
        new JsonLinesRangeTreeNode(_indexer, _reader, startIndex, count);

    /// <inheritdoc/>
    /// <remarks>
    /// Reads line bytes and creates individual line nodes for the current range.
    /// </remarks>
    protected override void AddDirectChildren()
    {
        var (byteOffset, rowOffset) = _indexer.GetCheckPoint(StartIndex);
        var lines = _reader.ReadLines(byteOffset, rowOffset, (int)Count);
        List<ITreeNode> children = [];

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].IsEmpty)
            {
                continue;
            }

            children.Add(CreateLineNode(lines[i], StartIndex + i));
        }

        Children = children;
    }
}

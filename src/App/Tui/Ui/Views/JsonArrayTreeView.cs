using Refedle.App.Tui.Ui.Views.JsonRangeTreeNodes;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonArray;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views;

/// <summary>
/// <see cref="RangeTreeViewBase"/> subclass for JSON Array files.
/// Creates <see cref="ElementReader"/> and populates the tree root.
/// Supports lazy loading with progressive node addition for large files.
/// </summary>
internal sealed class JsonArrayTreeView : RangeTreeViewBase
{
    private readonly ElementReader _reader;

    private JsonArrayTreeView(
        IRowIndexer indexer,
        ElementReader reader,
        Action onTableModeToggle,
        Action<ITreeNode?> onSelectionChanged,
        Action<Action> uiThreadInvoke,
        long nodeGroupSize)
        : base(indexer, onTableModeToggle, onSelectionChanged, uiThreadInvoke, nodeGroupSize)
    {
        _reader = reader;
    }

    /// <summary>
    /// Factory method that creates a <see cref="JsonArrayTreeView"/> for the given JSON Array file.
    /// </summary>
    /// <param name="indexer">The row indexer for the JSON Array file.</param>
    /// <param name="onTableModeToggle">Callback invoked when the user presses 't'.</param>
    /// <param name="onPathChanged">Callback invoked with the current selection's KeyPath whenever the cursor moves.</param>
    /// <param name="uiThreadInvoke">Marshals actions to the UI thread for thread-safe <c>AddObject</c> calls.</param>
    /// <returns>A new <see cref="JsonArrayTreeView"/> instance populated with element or range nodes.</returns>
    internal static JsonArrayTreeView Create(
        IRowIndexer indexer,
        Action onTableModeToggle,
        Action<IReadOnlyList<KeyPathSegment>> onPathChanged,
        Action<Action> uiThreadInvoke)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(onTableModeToggle);
        ArgumentNullException.ThrowIfNull(onPathChanged);
        ArgumentNullException.ThrowIfNull(uiThreadInvoke);

        var nodeGroupSize = RangePartitionPolicy.GetNodeGroupSize(indexer.FileSize);
        var view = new JsonArrayTreeView(
            indexer,
            new ElementReader(indexer.FilePath),
            onTableModeToggle,
            node => onPathChanged(node is null ? [] : KeyPathBuilder.Build(node)),
            uiThreadInvoke,
            nodeGroupSize);

        if (indexer.IsIndexingCompleted)
        {
            view.BuildInitialNodes(indexer.TotalRows);
            return view;
        }

        view.StartProgressiveLoading();

        return view;
    }

    /// <inheritdoc/>
    protected override RangeTreeNodeBase CreateRangeNode(long startIndex, long count) =>
        new JsonArrayRangeTreeNode(Indexer, _reader, startIndex, count);

    /// <inheritdoc/>
    protected override void AddSmallFileNodes(long totalRows)
    {
        var (byteOffset, rowOffset) = Indexer.GetCheckPoint(0);
        var elements = _reader.ReadElements(byteOffset, rowOffset, (int)totalRows);

        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i].IsEmpty)
            {
                continue;
            }

            AddObject(JsonArrayRangeTreeNode.CreateElementNode(elements[i], i));
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _reader.Dispose();
        }

        base.Dispose(disposing);
    }
}

using Refedle.App.Tui.Ui.Views.JsonRangeTreeNodes;
using Refedle.App.Tui.Ui.Views.JsonTreeNodes;
using Refedle.Engine.IO;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views;

/// <summary>
/// Abstract base for range-based tree views (<see cref="JsonArrayTreeView"/>, <see cref="JsonLinesTreeView"/>).
/// Handles progressive node loading, batch node addition, and event subscription lifecycle.
/// </summary>
internal abstract class RangeTreeViewBase : MorphTreeView
{
    private readonly Action<Action> _uiThreadInvoke;
    private readonly long _nodeGroupSize;
    private readonly Action<long, long> _progressHandler;
    private readonly Action _completedHandler;
    private long _addedSuperRangeNodeCount;
    private int _finalHandled;

    protected RangeTreeViewBase(
        IRowIndexer indexer,
        Action onTableModeToggle,
        Action<ITreeNode?> onSelectionChanged,
        Action<Action> uiThreadInvoke,
        long nodeGroupSize)
        : base(onTableModeToggle, onSelectionChanged)
    {
        Indexer = indexer;
        _uiThreadInvoke = uiThreadInvoke;
        _nodeGroupSize = nodeGroupSize;
        _progressHandler = (_, _) => AddNodesBatch(isFinal: false);
        _completedHandler = () => AddNodesBatch(isFinal: true);
        TreeBuilder = new DelegateTreeBuilder<ITreeNode>(
            // canExpand = branch/leaf (shows expand/collapse icon); IsExpanded = current expansion state (open/closed).
            canExpand: node =>
                // Type-based check only — never access Children here.
                // RangeTreeNodeBase: always expandable by design as it contains ≥ 1 element.
                // JsonObjectTreeNode/JsonArrayTreeNode: accessing Children would trigger an
                // immediate JSON parse of the element, bypassing lazy loading.
                node is RangeTreeNodeBase or JsonObjectTreeNode or JsonArrayTreeNode,
            childGetter: node =>
            {
                if (node is RangeTreeNodeBase r)
                {
                    r.EnsureChildrenLoaded();
                }

                return node.Children;
            }
        );
    }

    protected IRowIndexer Indexer { get; }

    internal void StartProgressiveLoading()
    {
        Indexer.ProgressChanged += _progressHandler;
        Indexer.BuildIndexCompleted += _completedHandler;

        if (Indexer.IsIndexingCompleted)
        {
            _completedHandler();
        }
    }

    internal void BuildInitialNodes(long totalRows)
    {
        if (totalRows <= 0)
        {
            return;
        }

        if (totalRows <= RangePartitionPolicy.RangeSize)
        {
            AddSmallFileNodes(totalRows);
            return;
        }

        var totalRangeNodes = (totalRows + _nodeGroupSize - 1) / _nodeGroupSize;
        for (var g = 0L; g < totalRangeNodes; g++)
        {
            var startIndex = g * _nodeGroupSize;
            var count = Math.Min(_nodeGroupSize, totalRows - startIndex);
            AddObject(CreateRangeNode(startIndex, count));
        }
    }

    private void AddNodesBatch(bool isFinal)
    {
        var currentRows = Indexer.TotalRows;
        var totalSuperRangeNodes = currentRows / _nodeGroupSize;
        var from = Volatile.Read(ref _addedSuperRangeNodeCount);

        if (from < totalSuperRangeNodes)
        {
            from = Interlocked.Exchange(ref _addedSuperRangeNodeCount, totalSuperRangeNodes);

            for (var g = from; g < totalSuperRangeNodes; g++)
            {
                var startIndex = g * _nodeGroupSize;
                _uiThreadInvoke(() => AddObject(CreateRangeNode(startIndex, _nodeGroupSize)));
            }
        }

        if (!isFinal)
        {
            return;
        }

        if (Interlocked.Exchange(ref _finalHandled, 1) != 0)
        {
            return;
        }

        var remainder = currentRows % _nodeGroupSize;
        if (remainder > 0)
        {
            _uiThreadInvoke(() =>
                AddObject(CreateRangeNode(totalSuperRangeNodes * _nodeGroupSize, remainder)));
        }
    }

    protected abstract RangeTreeNodeBase CreateRangeNode(long startIndex, long count);

    protected abstract void AddSmallFileNodes(long totalRows);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Indexer.ProgressChanged -= _progressHandler;
            Indexer.BuildIndexCompleted -= _completedHandler;
        }

        base.Dispose(disposing);
    }
}

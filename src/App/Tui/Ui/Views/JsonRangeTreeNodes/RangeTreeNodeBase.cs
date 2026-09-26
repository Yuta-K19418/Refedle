using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views.JsonRangeTreeNodes;

/// <summary>
/// Abstract base class for range-based tree nodes (<see cref="JsonLinesRangeTreeNode"/>,
/// <see cref="JsonArrayRangeTreeNode"/>). Provides a lazy-loading template method for children.
/// Range sizing logic lives in <see cref="RangePartitionPolicy"/>.
/// </summary>
/// <remarks>
/// Accessing <c>Children</c> does not trigger lazy loading.
/// <see cref="DelegateTreeBuilder{ITreeNode}"/> controls load timing via
/// <see cref="EnsureChildrenLoaded"/> to prevent <c>TreeView.AddObject()</c> from
/// triggering eager child retrieval.
/// </remarks>
internal abstract class RangeTreeNodeBase : TreeNode
{
    protected RangeTreeNodeBase(long startIndex, long count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        StartIndex = startIndex;
        Count = count;
    }

    protected long StartIndex { get; }
    protected long Count { get; }

    /// <summary>
    /// Whether children have been loaded at least once.
    /// Used by <see cref="DelegateTreeBuilder{ITreeNode}"/> to determine expand-arrow visibility
    /// without touching <c>Children</c> (which would trigger premature loading).
    /// </summary>
    internal bool IsChildrenLoaded { get; private set; }

    /// <summary>
    /// Loads children from the reader if not already loaded.
    /// Called by the <see cref="DelegateTreeBuilder{ITreeNode}"/> child getter on expansion.
    /// </summary>
    /// <remarks>
    /// This check-then-load sequence is intentionally unsynchronized. <see cref="DelegateTreeBuilder{ITreeNode}"/>
    /// invokes its child getter exclusively on the single UI thread, so <see cref="EnsureChildrenLoaded"/> can never
    /// be entered concurrently and <see cref="LoadChildren"/> cannot run twice for the same node. If this node is ever
    /// expanded from a non-UI thread, guarding this method with a lock becomes mandatory.
    /// </remarks>
    internal void EnsureChildrenLoaded()
    {
        if (IsChildrenLoaded)
        {
            return;
        }

        LoadChildren();
        IsChildrenLoaded = true;
    }

    /// <summary>
    /// Creates a child range node covering [<paramref name="startIndex"/>, <paramref name="startIndex"/> + <paramref name="count"/>).
    /// </summary>
    protected abstract RangeTreeNodeBase CreateRangeNode(long startIndex, long count);

    /// <summary>
    /// Populates <c>Children</c> with individual leaf nodes for the current range.
    /// </summary>
    protected abstract void AddDirectChildren();

    private void LoadChildren()
    {
        if (Count > RangePartitionPolicy.RangeSize)
        {
            AddNestedRangeNodes();
            return;
        }

        AddDirectChildren();
    }

    private void AddNestedRangeNodes()
    {
        var childStart = StartIndex;
        var remaining = Count;
        List<ITreeNode> children = [];

        while (remaining > 0)
        {
            var childCount = Math.Min(remaining, RangePartitionPolicy.RangeSize);
            children.Add(CreateRangeNode(childStart, childCount));
            childStart += childCount;
            remaining -= childCount;
        }

        Children = children;
    }
}

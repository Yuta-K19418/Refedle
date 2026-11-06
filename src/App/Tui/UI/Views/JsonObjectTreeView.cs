using Refedle.App.Tui.UI.Views.JsonRangeTreeNodes;
using Refedle.App.Tui.UI.Views.JsonTreeNodes;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonObject;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI.Views;

/// <summary>
/// <see cref="MorphTreeView"/> subclass for JSON Object files.
/// Creates one root-level tree node per top-level key from <see cref="Engine.IO.JsonObject.TopLevelScanner"/> results,
/// or root-level <see cref="JsonObjectRangeTreeNode"/> range nodes when the key count exceeds
/// <see cref="RangePartitionPolicy.RangeSize"/> (adding thousands of root nodes directly makes
/// <c>TreeView.AddObject</c> super-linear in Terminal.Gui 2.5.0).
/// </summary>
internal sealed class JsonObjectTreeView : MorphTreeView
{
    private JsonObjectTreeView(Action onTableModeToggle, Action<ITreeNode?> onSelectionChanged)
        : base(onTableModeToggle, onSelectionChanged)
    {
        TreeBuilder = new DelegateTreeBuilder<ITreeNode>(
            // canExpand = branch/leaf (shows expand/collapse icon); IsExpanded = current expansion state (open/closed).
            canExpand: static node =>
                // Type-based check only — never access Children here: on JsonObjectTreeNode/
                // JsonArrayTreeNode it would parse the element immediately, bypassing lazy loading.
                // RangeTreeNodeBase is always expandable as it contains ≥ 1 element.
                node is RangeTreeNodeBase or JsonObjectTreeNode or JsonArrayTreeNode,
            childGetter: static node =>
            {
                if (node is RangeTreeNodeBase r)
                {
                    r.EnsureChildrenLoaded();
                }

                return node.Children;
            }
        );
    }

    /// <summary>
    /// Creates a new <see cref="JsonObjectTreeView"/> populated with root-level key nodes,
    /// or with root-level range nodes when <paramref name="entries"/> exceeds
    /// <see cref="RangePartitionPolicy.RangeSize"/> entries.
    /// </summary>
    /// <param name="entries">
    /// The key-value pairs returned by <see cref="Engine.IO.JsonObject.TopLevelScanner.Scan"/>.
    /// </param>
    /// <param name="onTableModeToggle">
    /// Callback invoked when the user presses 't' to toggle between tree and table mode.
    /// JSON Object does not support table mode, so callers pass a no-op.
    /// </param>
    /// <param name="onPathChanged">Callback invoked with the current selection's KeyPath whenever the cursor moves.</param>
    /// <returns>A populated <see cref="JsonObjectTreeView"/>.</returns>
    internal static JsonObjectTreeView Create(
        IReadOnlyList<JsonObjectEntry> entries,
        Action onTableModeToggle,
        Action<IReadOnlyList<KeyPathSegment>> onPathChanged)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(onTableModeToggle);
        ArgumentNullException.ThrowIfNull(onPathChanged);
        var view = new JsonObjectTreeView(
            onTableModeToggle,
            node => onPathChanged(node is null ? [] : KeyPathBuilder.Build(node)));

        if (entries.Count <= RangePartitionPolicy.RangeSize)
        {
            AddKeyNodesDirectly(view, entries);
            return view;
        }

        AddRangeNodes(view, entries);
        return view;
    }

    private static void AddKeyNodesDirectly(JsonObjectTreeView view, IReadOnlyList<JsonObjectEntry> entries) =>
        view.AddObjects(entries.Select(static entry => JsonObjectRangeTreeNode.CreateKeyNode(entry.Key, entry.Value)));

    private static void AddRangeNodes(JsonObjectTreeView view, IReadOnlyList<JsonObjectEntry> entries) =>
        view.AddObjects(
            Enumerable
                .Range(0, (entries.Count + RangePartitionPolicy.RangeSize - 1) / RangePartitionPolicy.RangeSize)
                .Select(i =>
                {
                    var start = i * RangePartitionPolicy.RangeSize;
                    var count = Math.Min(RangePartitionPolicy.RangeSize, entries.Count - start);
                    return (ITreeNode)new JsonObjectRangeTreeNode(entries, start, count);
                }));
}

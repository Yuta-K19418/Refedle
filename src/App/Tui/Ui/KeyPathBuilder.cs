using System.Diagnostics;
using Refedle.App.Tui.Ui.Views.JsonTreeNodes;
using Refedle.Engine.IO.DrillDown;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui;

/// <summary>
/// Builds an ordered KeyPath from a selected tree node by walking its <c>ParentNode</c> chain
/// up to the root. Extracted from <see cref="AppKeyHandler"/> so the tree-view <c>Create</c>
/// factories can compute a path without depending on the keyboard-shortcut handler.
/// </summary>
internal static class KeyPathBuilder
{
    /// <summary>
    /// Traverses the <c>ParentNode</c> chain from <paramref name="node"/> up to the root,
    /// collecting <c>KeyName</c> segments in bottom-up order, then reverses to produce a
    /// root-to-leaf KeyPath.
    /// </summary>
    /// <param name="node">The selected tree node to build the KeyPath from.</param>
    /// <returns>An ordered list of path segments from root to <paramref name="node"/>.</returns>
    internal static IReadOnlyList<KeyPathSegment> Build(ITreeNode node)
    {
        List<KeyPathSegment> segments = [];
        var current = node;

        while (current is JsonObjectTreeNode or JsonArrayTreeNode or JsonValueTreeNode)
        {
            var (keyName, parent) = current switch
            {
                JsonObjectTreeNode obj => (obj.KeyName, obj.ParentNode),
                JsonArrayTreeNode arr => (arr.KeyName, arr.ParentNode),
                JsonValueTreeNode val => (val.KeyName, val.ParentNode),
                _ => throw new UnreachableException(),
            };

            if (keyName is not null)
            {
                // An array element's parent is the JsonArrayTreeNode that labeled it "[n]"; every
                // other node is an object property. Tagging by parent type — not by the label text —
                // keeps a literal object key such as "[0]" from colliding with an index marker.
                var kind = parent is JsonArrayTreeNode ? KeyPathSegmentKind.Index : KeyPathSegmentKind.Key;
                segments.Add(new KeyPathSegment(keyName, kind));
            }

            current = parent;
        }

        segments.Reverse();
        return segments;
    }
}

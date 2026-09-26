using System.Text.Json;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views.JsonTreeNodes;

/// <summary>
/// Represents a JSON primitive value (string, number, boolean, null).
/// Has no children.
/// </summary>
internal sealed class JsonValueTreeNode : TreeNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonValueTreeNode"/> class.
    /// </summary>
    /// <param name="text">The display text for this value node.</param>
    public JsonValueTreeNode(string text)
    {
        Text = text;
        Children = [];
    }

    /// <summary>
    /// Gets or initializes the JSON value kind of this node.
    /// </summary>
    public JsonValueKind ValueKind { get; init; }

    /// <summary>Property name this node represents. Null for root-level nodes.</summary>
    public string? KeyName { get; init; }

    /// <summary>Parent node in the tree, used to build a KeyPath via upward traversal. Null for root nodes.</summary>
    public ITreeNode? ParentNode { get; init; }
}

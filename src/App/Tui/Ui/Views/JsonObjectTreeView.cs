using System.Text.Json;
using Refedle.App.Tui.Ui.Views.JsonTreeNodes;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonObject;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views;

/// <summary>
/// <see cref="MorphTreeView"/> subclass for JSON Object files.
/// Creates one root-level tree node per top-level key from <see cref="Engine.IO.JsonObject.TopLevelScanner"/> results.
/// </summary>
internal sealed class JsonObjectTreeView : MorphTreeView
{
    private JsonObjectTreeView(Action onTableModeToggle, Action<ITreeNode?> onSelectionChanged)
        : base(onTableModeToggle, onSelectionChanged) { }

    /// <summary>
    /// Creates a new <see cref="JsonObjectTreeView"/> populated with root-level key nodes.
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
        foreach (var (key, valueBytes) in entries)
        {
            view.AddObject(CreateKeyNode(key, valueBytes));
        }

        return view;
    }

    /// <summary>
    /// Creates a single tree node for a top-level key-value pair.
    /// Mirrors <see cref="JsonRangeTreeNodes.JsonArrayRangeTreeNode.CreateElementNode"/>:
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
            if (!reader.Read())
            {
                return invalidNode();
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                return new JsonObjectTreeNode(valueBytes, prefix) { KeyName = key };
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                return new JsonArrayTreeNode(valueBytes, prefix) { KeyName = key };
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
}

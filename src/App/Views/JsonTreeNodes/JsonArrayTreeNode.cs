using System.Globalization;
using System.Text.Json;
using Refedle.Engine.IO.Json;
using Terminal.Gui.Views;

namespace Refedle.App.Views.JsonTreeNodes;

/// <summary>
/// Represents a JSON array node in the tree.
/// Supports lazy loading of children on first access.
/// </summary>
internal sealed class JsonArrayTreeNode : TreeNode
{
    private readonly JsonRawBytes _rawJson;
    private bool _childrenLoaded;

    /// <summary>Property name or index-label this node represents (e.g. "tags", "[0]"). Null for root-level array nodes.</summary>
    public string? KeyName { get; init; }

    /// <summary>Ancestor root record position. Same semantics as JsonObjectTreeNode.RecordPosition.</summary>
    public long? RecordPosition { get; init; }

    /// <summary>Parent node in the tree, used to build a KeyPath via upward traversal. Null for root nodes.</summary>
    public ITreeNode? ParentNode { get; init; }

    /// <summary>Raw JSON bytes of this node.</summary>
    internal JsonRawBytes RawJson => _rawJson;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonArrayTreeNode"/> class.
    /// </summary>
    /// <param name="rawJson">The raw JSON bytes representing this array.</param>
    /// <param name="prefix">Optional prefix prepended to the display text (e.g. "Line N: ").</param>
    public JsonArrayTreeNode(JsonRawBytes rawJson, string prefix = "")
    {
        _rawJson = rawJson;
        Text = $"{prefix}{FormatDisplayText()}";
    }

    /// <inheritdoc/>
    public override IList<ITreeNode> Children
    {
        get
        {
            if (!_childrenLoaded)
            {
                LoadChildren();
                _childrenLoaded = true;
            }

            return base.Children;
        }
        set => base.Children = value;
    }

    private void LoadChildren()
    {
        List<ITreeNode> children = [];

        var reader = new Utf8JsonReader(_rawJson.Span);

        bool isValidArrayStart;
        try
        {
            isValidArrayStart = reader.Read() && reader.TokenType == JsonTokenType.StartArray;
        }
        catch (JsonException)
        {
            isValidArrayStart = false;
        }

        if (!isValidArrayStart)
        {
            children.Add(
                new JsonValueTreeNode("[Invalid JSON Array]")
                {
                    ValueKind = JsonValueKind.Undefined,
                }
            );
            base.Children = children;
            return;
        }

        var elementIndex = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            var elementNode = JsonTreeNodeHelper.CreateChildNode(
                ref reader,
                string.Create(CultureInfo.InvariantCulture, $"[{elementIndex}]"),
                _rawJson,
                RecordPosition,
                this
            );
            if (elementNode is not null)
            {
                children.Add(elementNode);
            }

            elementIndex++;
        }

        base.Children = children;
    }

    private string FormatDisplayText()
    {
        var reader = new Utf8JsonReader(_rawJson.Span);

        bool isValidArrayStart;
        try
        {
            isValidArrayStart = reader.Read() && reader.TokenType == JsonTokenType.StartArray;
        }
        catch (JsonException)
        {
            isValidArrayStart = false;
        }

        if (!isValidArrayStart)
        {
            return "[Invalid Array]";
        }

        return JsonByteExtractor.FormatArrayPreview(ref reader);
    }
}

using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.JsonTreeNodes;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views.JsonTreeNodes;

/// <summary>
/// Tests for the <see cref="Refedle.App.Tui.UI.Views.JsonTreeNodes.JsonTreeNodeHelper"/> class.
/// </summary>
public sealed class JsonTreeNodeHelperTests
{
    [Fact]
    public void CreateChildNode_WithStartObjectToken_ReturnsJsonObjectTreeNode()
    {
        // Arrange
        string json = "{\"key\": \"value\"}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "obj", rawJson);

        // Assert
        node.Should().NotBeNull();
        node.Should().BeOfType<JsonObjectTreeNode>();
        node.Text.Should().Be("obj: {Object: 1 properties}");
    }

    [Fact]
    public void CreateChildNode_WithStartArrayToken_ReturnsJsonArrayTreeNode()
    {
        // Arrange
        string json = "[1, 2, 3]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "arr", rawJson);

        // Assert
        node.Should().NotBeNull();
        node.Should().BeOfType<JsonArrayTreeNode>();
        node.Text.Should().Be("arr: [Array: 3 items]");
    }

    [Fact]
    public void CreateChildNode_WithStringToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        string json = "\"hello\"";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to String

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "str", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        JsonValueTreeNode valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("str: \"hello\"");
        valueNode.ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public void CreateChildNode_WithNumberToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        string json = "123";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to Number

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "num", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        JsonValueTreeNode valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("num: 123");
        valueNode.ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public void CreateChildNode_WithTrueToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        string json = "true";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to True

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "bool", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        JsonValueTreeNode valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("bool: true");
        valueNode.ValueKind.Should().Be(JsonValueKind.True);
    }

    [Fact]
    public void CreateChildNode_WithFalseToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        string json = "false";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to False

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "bool", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        JsonValueTreeNode valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("bool: false");
        valueNode.ValueKind.Should().Be(JsonValueKind.False);
    }

    [Fact]
    public void CreateChildNode_WithNullToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        string json = "null";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to Null

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "nul", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        JsonValueTreeNode valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("nul: <null>");
        valueNode.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void CreateChildNode_WithUnrecognizedToken_ReturnsUndefinedJsonValueTreeNode()
    {
        // Arrange
        string json = "{}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartObject
        reader.Read(); // Move to EndObject (unhandled in switch)

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "unk", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        JsonValueTreeNode valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("unk: <unknown>");
        valueNode.ValueKind.Should().Be(JsonValueKind.Undefined);
    }

    [Theory]
    [InlineData("Hello World", "Hello World")]
    [InlineData("Line 1\nLine 2", "Line 1\\nLine 2")]
    [InlineData("Value with\rreturn", "Value with\\rreturn")]
    [InlineData("Indented\ttext", "Indented\\ttext")]
    [InlineData("A \"quoted\" string", "A \\\"quoted\\\" string")]
    [InlineData("All\r\n\t\"special\"\r\nchars", "All\\r\\n\\t\\\"special\\\"\\r\\nchars")]
    public void EscapeString_WithVariousInputs_ReturnsCorrectlyEscapedString(
        string input,
        string expected
    )
    {
        // Arrange

        // Act
        string result = JsonTreeNodeHelper.EscapeString(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CreateChildNode_NestedObject_SetsKeyNameOnReturnedNode()
    {
        // Arrange
        string json = "{\"key\": \"value\"}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "obj", rawJson);

        // Assert
        JsonObjectTreeNode objectNode = node.Should().BeOfType<JsonObjectTreeNode>().Subject;
        objectNode.KeyName.Should().Be("obj");
    }

    [Fact]
    public void CreateChildNode_NestedArray_SetsKeyNameOnReturnedNode()
    {
        // Arrange
        string json = "[1, 2, 3]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "arr", rawJson);

        // Assert
        JsonArrayTreeNode arrayNode = node.Should().BeOfType<JsonArrayTreeNode>().Subject;
        arrayNode.KeyName.Should().Be("arr");
    }

    [Fact]
    public void CreateChildNode_WithRecordPosition_PropagatesRecordPositionToObjectNode()
    {
        // Arrange
        string json = "{\"key\": \"value\"}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "obj", rawJson, recordPosition: 5L);

        // Assert
        JsonObjectTreeNode objectNode = node.Should().BeOfType<JsonObjectTreeNode>().Subject;
        objectNode.RecordPosition.Should().Be(5L);
    }

    [Fact]
    public void CreateChildNode_WithRecordPosition_PropagatesRecordPositionToArrayNode()
    {
        // Arrange
        string json = "[1, 2, 3]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "arr", rawJson, recordPosition: 7L);

        // Assert
        JsonArrayTreeNode arrayNode = node.Should().BeOfType<JsonArrayTreeNode>().Subject;
        arrayNode.RecordPosition.Should().Be(7L);
    }

    [Fact]
    public void CreateChildNode_WithNullRecordPosition_SetsNullRecordPositionOnNode()
    {
        // Arrange
        string json = "{\"key\": \"value\"}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "obj", rawJson);

        // Assert
        JsonObjectTreeNode objectNode = node.Should().BeOfType<JsonObjectTreeNode>().Subject;
        objectNode.RecordPosition.Should().BeNull();
    }

    [Fact]
    public void CreateChildNode_WithObjectToken_PropagatesParentNodeAndKeyName()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("{\"k\":\"v\"}");
        Utf8JsonReader reader = new(rawJson);
        reader.Read();
        ITreeNode parent = new JsonValueTreeNode("parent");

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "obj", rawJson, parentNode: parent);

        // Assert
        JsonObjectTreeNode objectNode = node.Should().BeOfType<JsonObjectTreeNode>().Subject;
        objectNode.KeyName.Should().Be("obj");
        objectNode.ParentNode.Should().BeSameAs(parent);
    }

    [Fact]
    public void CreateChildNode_WithArrayToken_PropagatesParentNodeAndKeyName()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("[1]");
        Utf8JsonReader reader = new(rawJson);
        reader.Read();
        ITreeNode parent = new JsonValueTreeNode("parent");

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, "arr", rawJson, parentNode: parent);

        // Assert
        JsonArrayTreeNode arrayNode = node.Should().BeOfType<JsonArrayTreeNode>().Subject;
        arrayNode.KeyName.Should().Be("arr");
        arrayNode.ParentNode.Should().BeSameAs(parent);
    }

    [Theory]
    [InlineData("\"s\"", "str")]
    [InlineData("1", "num")]
    [InlineData("true", "t")]
    [InlineData("false", "f")]
    [InlineData("null", "nul")]
    public void CreateChildNode_WithValueToken_PropagatesParentNodeAndKeyName(string json, string label)
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read();
        ITreeNode parent = new JsonValueTreeNode("parent");

        // Act
        ITreeNode? node = JsonTreeNodeHelper.CreateChildNode(ref reader, label, rawJson, parentNode: parent);

        // Assert
        JsonValueTreeNode valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.KeyName.Should().Be(label);
        valueNode.ParentNode.Should().BeSameAs(parent);
    }
}

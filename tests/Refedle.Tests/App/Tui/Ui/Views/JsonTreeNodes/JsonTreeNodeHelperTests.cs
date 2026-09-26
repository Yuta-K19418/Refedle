using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views.JsonTreeNodes;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.Ui.Views.JsonTreeNodes;

/// <summary>
/// Tests for the <see cref="Refedle.App.Tui.Ui.Views.JsonTreeNodes.JsonTreeNodeHelper"/> class.
/// </summary>
public sealed class JsonTreeNodeHelperTests
{
    [Fact]
    public void CreateChildNode_WithStartObjectToken_ReturnsJsonObjectTreeNode()
    {
        // Arrange
        var json = "{\"key\": \"value\"}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "obj", rawJson);

        // Assert
        node.Should().NotBeNull();
        node.Should().BeOfType<JsonObjectTreeNode>();
        node.Text.Should().Be("obj: {Object: 1 properties}");
    }

    [Fact]
    public void CreateChildNode_WithStartArrayToken_ReturnsJsonArrayTreeNode()
    {
        // Arrange
        var json = "[1, 2, 3]";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "arr", rawJson);

        // Assert
        node.Should().NotBeNull();
        node.Should().BeOfType<JsonArrayTreeNode>();
        node.Text.Should().Be("arr: [Array: 3 items]");
    }

    [Fact]
    public void CreateChildNode_WithStringToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        var json = "\"hello\"";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to String

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "str", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        var valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("str: \"hello\"");
        valueNode.ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public void CreateChildNode_WithNumberToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        var json = "123";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to Number

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "num", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        var valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("num: 123");
        valueNode.ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public void CreateChildNode_WithTrueToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        var json = "true";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to True

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "bool", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        var valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("bool: true");
        valueNode.ValueKind.Should().Be(JsonValueKind.True);
    }

    [Fact]
    public void CreateChildNode_WithFalseToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        var json = "false";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to False

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "bool", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        var valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("bool: false");
        valueNode.ValueKind.Should().Be(JsonValueKind.False);
    }

    [Fact]
    public void CreateChildNode_WithNullToken_ReturnsJsonValueTreeNode()
    {
        // Arrange
        var json = "null";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to Null

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "nul", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        var valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.Text.Should().Be("nul: <null>");
        valueNode.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void CreateChildNode_WithUnrecognizedToken_ReturnsUndefinedJsonValueTreeNode()
    {
        // Arrange
        var json = "{}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartObject
        reader.Read(); // Move to EndObject (unhandled in switch)

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "unk", rawJson);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        var valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
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
        var result = JsonTreeNodeHelper.EscapeString(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CreateChildNode_NestedObject_SetsKeyNameOnReturnedNode()
    {
        // Arrange
        var json = "{\"key\": \"value\"}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "obj", rawJson);

        // Assert
        var objectNode = node.Should().BeOfType<JsonObjectTreeNode>().Subject;
        objectNode.KeyName.Should().Be("obj");
    }

    [Fact]
    public void CreateChildNode_NestedArray_SetsKeyNameOnReturnedNode()
    {
        // Arrange
        var json = "[1, 2, 3]";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "arr", rawJson);

        // Assert
        var arrayNode = node.Should().BeOfType<JsonArrayTreeNode>().Subject;
        arrayNode.KeyName.Should().Be("arr");
    }

    [Fact]
    public void CreateChildNode_WithRecordPosition_PropagatesRecordPositionToObjectNode()
    {
        // Arrange
        var json = "{\"key\": \"value\"}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "obj", rawJson, recordPosition: 5L);

        // Assert
        var objectNode = node.Should().BeOfType<JsonObjectTreeNode>().Subject;
        objectNode.RecordPosition.Should().Be(5L);
    }

    [Fact]
    public void CreateChildNode_WithRecordPosition_PropagatesRecordPositionToArrayNode()
    {
        // Arrange
        var json = "[1, 2, 3]";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "arr", rawJson, recordPosition: 7L);

        // Assert
        var arrayNode = node.Should().BeOfType<JsonArrayTreeNode>().Subject;
        arrayNode.RecordPosition.Should().Be(7L);
    }

    [Fact]
    public void CreateChildNode_WithNullRecordPosition_SetsNullRecordPositionOnNode()
    {
        // Arrange
        var json = "{\"key\": \"value\"}";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "obj", rawJson);

        // Assert
        var objectNode = node.Should().BeOfType<JsonObjectTreeNode>().Subject;
        objectNode.RecordPosition.Should().BeNull();
    }

    [Fact]
    public void CreateChildNode_WithObjectToken_PropagatesParentNodeAndKeyName()
    {
        // Arrange
        var rawJson = Encoding.UTF8.GetBytes("{\"k\":\"v\"}");
        var reader = new Utf8JsonReader(rawJson);
        reader.Read();
        ITreeNode parent = new JsonValueTreeNode("parent");

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "obj", rawJson, parentNode: parent);

        // Assert
        var objectNode = node.Should().BeOfType<JsonObjectTreeNode>().Subject;
        objectNode.KeyName.Should().Be("obj");
        objectNode.ParentNode.Should().BeSameAs(parent);
    }

    [Fact]
    public void CreateChildNode_WithArrayToken_PropagatesParentNodeAndKeyName()
    {
        // Arrange
        var rawJson = Encoding.UTF8.GetBytes("[1]");
        var reader = new Utf8JsonReader(rawJson);
        reader.Read();
        ITreeNode parent = new JsonValueTreeNode("parent");

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, "arr", rawJson, parentNode: parent);

        // Assert
        var arrayNode = node.Should().BeOfType<JsonArrayTreeNode>().Subject;
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
        var rawJson = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(rawJson);
        reader.Read();
        ITreeNode parent = new JsonValueTreeNode("parent");

        // Act
        var node = JsonTreeNodeHelper.CreateChildNode(ref reader, label, rawJson, parentNode: parent);

        // Assert
        var valueNode = node.Should().BeOfType<JsonValueTreeNode>().Subject;
        valueNode.KeyName.Should().Be(label);
        valueNode.ParentNode.Should().BeSameAs(parent);
    }
}

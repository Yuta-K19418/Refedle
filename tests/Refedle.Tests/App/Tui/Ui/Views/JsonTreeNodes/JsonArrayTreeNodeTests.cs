using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views.JsonTreeNodes;

namespace Refedle.Tests.App.Tui.Ui.Views.JsonTreeNodes;

/// <summary>
/// Tests for the <see cref="JsonArrayTreeNode"/> class.
/// </summary>
public sealed class JsonArrayTreeNodeTests
{
    [Fact]
    public void Constructor_WithValidJsonArray_SetsCorrectDisplayText()
    {
        // Arrange
        var json = "[1, 2, 3]";
        var rawJson = Encoding.UTF8.GetBytes(json);

        // Act
        var node = new JsonArrayTreeNode(rawJson);

        // Assert
        node.Text.Should().Be("[Array: 3 items]");
    }

    [Fact]
    public void Constructor_WithEmptyJsonArray_SetsCorrectDisplayTextForZeroItems()
    {
        // Arrange
        var json = "[]";
        var rawJson = Encoding.UTF8.GetBytes(json);

        // Act
        var node = new JsonArrayTreeNode(rawJson);

        // Assert
        node.Text.Should().Be("[Array: 0 items]");
    }

    [Fact]
    public void Constructor_WithInvalidJson_SetsInvalidArrayDisplayText()
    {
        // Arrange
        var json = "not an array";
        var rawJson = Encoding.UTF8.GetBytes(json);

        // Act
        var node = new JsonArrayTreeNode(rawJson);

        // Assert
        node.Text.Should().Be("[Invalid Array]");
    }

    [Fact]
    public void Children_OnFirstAccess_LoadsChildNodes()
    {
        // Arrange
        var json = "[1, 2]";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonArrayTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(2);
    }

    [Fact]
    public void Children_OnSubsequentAccess_DoesNotReloadChildNodes()
    {
        // Arrange
        var json = "[1]";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonArrayTreeNode(rawJson);

        // Act
        var firstAccess = node.Children;
        var secondAccess = node.Children;

        // Assert
        firstAccess.Should().BeSameAs(secondAccess);
    }

    [Fact]
    public void Children_ForEmptyArray_ReturnsEmptyList()
    {
        // Arrange
        var json = "[]";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonArrayTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().BeEmpty();
    }

    [Fact]
    public void Children_ForArrayWithPrimitiveValues_CreatesCorrectValueNodes()
    {
        // Arrange
        var json = "[\"text\", 123, true, null]";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonArrayTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(4);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.String);

        children[1].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.Number);
        children[2].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.True);
        children[3].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Children_ForArrayWithNestedObject_CreatesJsonObjectNode()
    {
        // Arrange
        var json = "[{}]";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonArrayTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>();
    }

    [Fact]
    public void Children_ForArrayWithNestedArray_CreatesJsonArrayNode()
    {
        // Arrange
        var json = "[[]]";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonArrayTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>();
    }

    [Fact]
    public void Children_ForArrayWithMixedContentTypes_CreatesCorrectNodes()
    {
        // Arrange
        var json = "[1, {}, []]";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonArrayTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(3);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[1].Should().BeOfType<JsonObjectTreeNode>();
        children[2].Should().BeOfType<JsonArrayTreeNode>();
    }

    [Fact]
    public void Children_ForInvalidJsonArray_ReturnsSingleErrorNode()
    {
        // Arrange
        var json = "invalid";
        var rawJson = Encoding.UTF8.GetBytes(json);
        var node = new JsonArrayTreeNode(rawJson);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].As<JsonValueTreeNode>().Text.Should().Be("[Invalid JSON Array]");
        children[0].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.Undefined);
    }

    [Fact]
    public void KeyName_IsSetViaInitializer_ReturnsExpectedKeyName()
    {
        // Arrange
        var rawJson = Encoding.UTF8.GetBytes("[1, 2, 3]");

        // Act
        var node = new JsonArrayTreeNode(rawJson) { KeyName = "tags" };

        // Assert
        node.KeyName.Should().Be("tags");
    }

    [Fact]
    public void RecordPosition_IsSetViaInitializer_ReturnsExpectedRecordPosition()
    {
        // Arrange
        var rawJson = Encoding.UTF8.GetBytes("[1, 2, 3]");

        // Act
        var node = new JsonArrayTreeNode(rawJson) { RecordPosition = 99 };

        // Assert
        node.RecordPosition.Should().Be(99);
    }

    [Fact]
    public void RawJson_ReturnsUnderlyingBytes()
    {
        // Arrange
        var rawJson = Encoding.UTF8.GetBytes("[1, 2]");

        // Act
        var node = new JsonArrayTreeNode(rawJson);

        // Assert
        node.RawJson.ToArray().Should().Equal(rawJson);
    }

    [Fact]
    public void LoadChildren_WithRecordPosition_PropagatesRecordPositionToChildObjectNodes()
    {
        // Arrange
        var rawJson = Encoding.UTF8.GetBytes("[{}]");

        // Act
        var node = new JsonArrayTreeNode(rawJson) { RecordPosition = 5 };
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>()
            .Which.RecordPosition.Should().Be(5);
    }

    [Fact]
    public void LoadChildren_WithRecordPosition_PropagatesRecordPositionToChildArrayNodes()
    {
        // Arrange
        var rawJson = Encoding.UTF8.GetBytes("[[]]");

        // Act
        var node = new JsonArrayTreeNode(rawJson) { RecordPosition = 5 };
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>()
            .Which.RecordPosition.Should().Be(5);
    }

    [Fact]
    public void LoadChildren_WithNullRecordPosition_PropagatesNullToChildObjectNodes()
    {
        // Arrange
        var rawJson = Encoding.UTF8.GetBytes("[{}]");

        // Act
        var node = new JsonArrayTreeNode(rawJson);
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>()
            .Which.RecordPosition.Should().BeNull();
    }

    [Fact]
    public void LoadChildren_WithNullRecordPosition_PropagatesNullToChildArrayNodes()
    {
        // Arrange
        var rawJson = Encoding.UTF8.GetBytes("[[]]");

        // Act
        var node = new JsonArrayTreeNode(rawJson);
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>()
            .Which.RecordPosition.Should().BeNull();
    }

    [Fact]
    public void LoadChildren_WithNestedObject_SetsKeyNameOnChildObjectNode()
    {
        // Arrange
        var rawJson = Encoding.UTF8.GetBytes("[{}]");

        // Act
        var node = new JsonArrayTreeNode(rawJson);
        var children = node.Children;

        // Assert — array element children carry their index label as KeyName (e.g. "[0]")
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>()
            .Which.KeyName.Should().Be("[0]");
    }

    [Fact]
    public void LoadChildren_WithNestedArray_SetsKeyNameOnChildArrayNode()
    {
        // Arrange
        var rawJson = Encoding.UTF8.GetBytes("[[]]");

        // Act
        var node = new JsonArrayTreeNode(rawJson);
        var children = node.Children;

        // Assert — array element children carry their index label as KeyName (e.g. "[0]")
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>()
            .Which.KeyName.Should().Be("[0]");
    }
}

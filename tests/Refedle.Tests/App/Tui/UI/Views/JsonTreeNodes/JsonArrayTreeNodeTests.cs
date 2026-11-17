using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.JsonTreeNodes;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views.JsonTreeNodes;

/// <summary>
/// Tests for the <see cref="JsonArrayTreeNode"/> class.
/// </summary>
public sealed class JsonArrayTreeNodeTests
{
    [Fact]
    public void Constructor_WithValidJsonArray_SetsCorrectDisplayText()
    {
        // Arrange
        string json = "[1, 2, 3]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);

        // Act
        JsonArrayTreeNode node = new(rawJson);

        // Assert
        node.Text.Should().Be("[Array: 3 items]");
    }

    [Fact]
    public void Constructor_WithEmptyJsonArray_SetsCorrectDisplayTextForZeroItems()
    {
        // Arrange
        string json = "[]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);

        // Act
        JsonArrayTreeNode node = new(rawJson);

        // Assert
        node.Text.Should().Be("[Array: 0 items]");
    }

    [Fact]
    public void Constructor_WithInvalidJson_SetsInvalidArrayDisplayText()
    {
        // Arrange
        string json = "not an array";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);

        // Act
        JsonArrayTreeNode node = new(rawJson);

        // Assert
        node.Text.Should().Be("[Invalid Array]");
    }

    [Fact]
    public void Children_OnFirstAccess_LoadsChildNodes()
    {
        // Arrange
        string json = "[1, 2]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonArrayTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(2);
    }

    [Fact]
    public void Children_OnSubsequentAccess_DoesNotReloadChildNodes()
    {
        // Arrange
        string json = "[1]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonArrayTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> firstAccess = node.Children;
        IList<ITreeNode> secondAccess = node.Children;

        // Assert
        firstAccess.Should().BeSameAs(secondAccess);
    }

    [Fact]
    public void Children_ForEmptyArray_ReturnsEmptyList()
    {
        // Arrange
        string json = "[]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonArrayTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().BeEmpty();
    }

    [Fact]
    public void Children_ForArrayWithPrimitiveValues_CreatesCorrectValueNodes()
    {
        // Arrange
        string json = "[\"text\", 123, true, null]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonArrayTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

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
        string json = "[{}]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonArrayTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>();
    }

    [Fact]
    public void Children_ForArrayWithNestedArray_CreatesJsonArrayNode()
    {
        // Arrange
        string json = "[[]]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonArrayTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>();
    }

    [Fact]
    public void Children_ForArrayWithMixedContentTypes_CreatesCorrectNodes()
    {
        // Arrange
        string json = "[1, {}, []]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonArrayTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

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
        string json = "invalid";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonArrayTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

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
        byte[] rawJson = Encoding.UTF8.GetBytes("[1, 2, 3]");

        // Act
        JsonArrayTreeNode node = new(rawJson) { KeyName = "tags" };

        // Assert
        node.KeyName.Should().Be("tags");
    }

    [Fact]
    public void RecordPosition_IsSetViaInitializer_ReturnsExpectedRecordPosition()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("[1, 2, 3]");

        // Act
        JsonArrayTreeNode node = new(rawJson) { RecordPosition = 99 };

        // Assert
        node.RecordPosition.Should().Be(99);
    }

    [Fact]
    public void RawJson_ReturnsUnderlyingBytes()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("[1, 2]");

        // Act
        JsonArrayTreeNode node = new(rawJson);

        // Assert
        node.RawJson.ToArray().Should().Equal(rawJson);
    }

    [Fact]
    public void LoadChildren_WithRecordPosition_PropagatesRecordPositionToChildObjectNodes()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("[{}]");

        // Act
        JsonArrayTreeNode node = new(rawJson) { RecordPosition = 5 };
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>()
            .Which.RecordPosition.Should().Be(5);
    }

    [Fact]
    public void LoadChildren_WithRecordPosition_PropagatesRecordPositionToChildArrayNodes()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("[[]]");

        // Act
        JsonArrayTreeNode node = new(rawJson) { RecordPosition = 5 };
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>()
            .Which.RecordPosition.Should().Be(5);
    }

    [Fact]
    public void LoadChildren_WithNullRecordPosition_PropagatesNullToChildObjectNodes()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("[{}]");

        // Act
        JsonArrayTreeNode node = new(rawJson);
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>()
            .Which.RecordPosition.Should().BeNull();
    }

    [Fact]
    public void LoadChildren_WithNullRecordPosition_PropagatesNullToChildArrayNodes()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("[[]]");

        // Act
        JsonArrayTreeNode node = new(rawJson);
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>()
            .Which.RecordPosition.Should().BeNull();
    }

    [Fact]
    public void LoadChildren_WithNestedObject_SetsKeyNameOnChildObjectNode()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("[{}]");

        // Act
        JsonArrayTreeNode node = new(rawJson);
        IList<ITreeNode> children = node.Children;

        // Assert — array element children carry their index label as KeyName (e.g. "[0]")
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>()
            .Which.KeyName.Should().Be("[0]");
    }

    [Fact]
    public void LoadChildren_WithNestedArray_SetsKeyNameOnChildArrayNode()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("[[]]");

        // Act
        JsonArrayTreeNode node = new(rawJson);
        IList<ITreeNode> children = node.Children;

        // Assert — array element children carry their index label as KeyName (e.g. "[0]")
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>()
            .Which.KeyName.Should().Be("[0]");
    }
}

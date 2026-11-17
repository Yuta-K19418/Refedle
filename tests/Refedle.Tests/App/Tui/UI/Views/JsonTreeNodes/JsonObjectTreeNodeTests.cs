using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.JsonTreeNodes;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views.JsonTreeNodes;

/// <summary>
/// Tests for the <see cref="JsonObjectTreeNode"/> class.
/// </summary>
public sealed class JsonObjectTreeNodeTests
{
    [Fact]
    public void Constructor_WithValidJsonObject_SetsCorrectDisplayText()
    {
        // Arrange
        string json = "{\"key\": \"value\"}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);

        // Act
        JsonObjectTreeNode node = new(rawJson);

        // Assert
        node.Text.Should().Be("{Object: 1 properties}");
    }

    [Fact]
    public void Constructor_WithEmptyJsonObject_SetsCorrectDisplayTextForZeroProperties()
    {
        // Arrange
        string json = "{}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);

        // Act
        JsonObjectTreeNode node = new(rawJson);

        // Assert
        node.Text.Should().Be("{Object: 0 properties}");
    }

    [Fact]
    public void Constructor_WithInvalidJson_SetsInvalidObjectDisplayText()
    {
        // Arrange
        string json = "not an object";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);

        // Act
        JsonObjectTreeNode node = new(rawJson);

        // Assert
        node.Text.Should().Be("{Invalid Object}");
    }

    [Fact]
    public void Children_OnFirstAccess_LoadsChildNodes()
    {
        // Arrange
        string json = "{\"a\": 1, \"b\": 2}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonObjectTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(2);
    }

    [Fact]
    public void Children_OnSubsequentAccess_DoesNotReloadChildNodes()
    {
        // Arrange
        string json = "{\"a\": 1}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonObjectTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> firstAccess = node.Children;
        IList<ITreeNode> secondAccess = node.Children;

        // Assert
        firstAccess.Should().BeSameAs(secondAccess);
    }

    [Fact]
    public void Children_ForEmptyObject_ReturnsEmptyList()
    {
        // Arrange
        string json = "{}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonObjectTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().BeEmpty();
    }

    [Fact]
    public void Children_ForObjectWithPrimitiveProperties_CreatesCorrectValueNodes()
    {
        // Arrange
        string json = "{\"str\": \"text\", \"num\": 123, \"bool\": true, \"null\": null}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonObjectTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(4);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].As<JsonValueTreeNode>().Text.Should().Contain("str");
        children[0].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.String);

        children[1].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.Number);
        children[2].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.True);
        children[3].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Children_ForObjectWithNestedObject_CreatesJsonObjectNode()
    {
        // Arrange
        string json = "{\"nested\": {}}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonObjectTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>();
    }

    [Fact]
    public void Children_ForObjectWithNestedArray_CreatesJsonArrayNode()
    {
        // Arrange
        string json = "{\"arr\": []}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonObjectTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>();
    }

    [Fact]
    public void Children_ForObjectWithMixedProperties_CreatesCorrectNodes()
    {
        // Arrange
        string json = "{\"a\": 1, \"b\": {}, \"c\": []}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonObjectTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(3);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[1].Should().BeOfType<JsonObjectTreeNode>();
        children[2].Should().BeOfType<JsonArrayTreeNode>();
    }

    [Fact]
    public void Children_ForInvalidJsonObject_ReturnsSingleErrorNode()
    {
        // Arrange
        string json = "invalid";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonObjectTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].As<JsonValueTreeNode>().Text.Should().Be("[Invalid JSON Object]");
        children[0].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.Undefined);
    }

    [Fact]
    public void Children_ForJsonObjectWithMissingValue_HandlesGracefully()
    {
        // Arrange
        string json = "{\"key\": null}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        JsonObjectTreeNode node = new(rawJson);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].As<JsonValueTreeNode>().Text.Should().Contain("key");
        children[0].As<JsonValueTreeNode>().ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void KeyName_IsSetViaInitializer_ReturnsExpectedKeyName()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("{\"key\": \"value\"}");

        // Act
        JsonObjectTreeNode node = new(rawJson) { KeyName = "name" };

        // Assert
        node.KeyName.Should().Be("name");
    }

    [Fact]
    public void RecordPosition_IsSetViaInitializer_ReturnsExpectedRecordPosition()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("{\"key\": \"value\"}");

        // Act
        JsonObjectTreeNode node = new(rawJson) { RecordPosition = 42 };

        // Assert
        node.RecordPosition.Should().Be(42);
    }

    [Fact]
    public void LoadChildren_WithRecordPosition_PropagatesRecordPositionToChildObjectNodes()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("{\"nested\": {}}");

        // Act
        JsonObjectTreeNode node = new(rawJson) { RecordPosition = 7 };
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>()
            .Which.RecordPosition.Should().Be(7);
    }

    [Fact]
    public void LoadChildren_WithRecordPosition_PropagatesRecordPositionToChildArrayNodes()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("{\"arr\": []}");

        // Act
        JsonObjectTreeNode node = new(rawJson) { RecordPosition = 7 };
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>()
            .Which.RecordPosition.Should().Be(7);
    }

    [Fact]
    public void LoadChildren_WithNullRecordPosition_PropagatesNullToChildObjectNodes()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("{\"nested\": {}}");

        // Act
        JsonObjectTreeNode node = new(rawJson);
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
        byte[] rawJson = Encoding.UTF8.GetBytes("{\"arr\": []}");

        // Act
        JsonObjectTreeNode node = new(rawJson);
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
        byte[] rawJson = Encoding.UTF8.GetBytes("{\"nested\": {}}");

        // Act
        JsonObjectTreeNode node = new(rawJson);
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>()
            .Which.KeyName.Should().Be("nested");
    }

    [Fact]
    public void LoadChildren_WithNestedArray_SetsKeyNameOnChildArrayNode()
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes("{\"arr\": []}");

        // Act
        JsonObjectTreeNode node = new(rawJson);
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>()
            .Which.KeyName.Should().Be("arr");
    }
}

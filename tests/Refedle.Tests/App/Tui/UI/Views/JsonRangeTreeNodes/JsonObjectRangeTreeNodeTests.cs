using System.Globalization;
using System.Text;
using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.JsonRangeTreeNodes;
using Refedle.App.Tui.UI.Views.JsonTreeNodes;
using Refedle.Engine.IO.JsonObject;

namespace Refedle.Tests.App.Tui.UI.Views.JsonRangeTreeNodes;

public sealed class JsonObjectRangeTreeNodeTests
{
    private static JsonRawBytes ToBytes(string json) =>
        Encoding.UTF8.GetBytes(json);

    private static List<JsonObjectEntry> CreateNumberedEntries(int count) =>
        [.. Enumerable
            .Range(0, count)
            .Select(i => new JsonObjectEntry(
                string.Create(CultureInfo.InvariantCulture, $"k{i}"),
                ToBytes(i.ToString(CultureInfo.InvariantCulture))))];

    // --- Constructor tests ---

    [Fact]
    public void Constructor_WithNullEntries_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<JsonObjectEntry>? nullEntries = null;

        // Act
        var act = () => new JsonObjectRangeTreeNode(nullEntries!, 0L, 10L);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNegativeStartIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var entries = CreateNumberedEntries(1);

        // Act
        var act = () => new JsonObjectRangeTreeNode(entries, -1L, 10L);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var entries = CreateNumberedEntries(1);

        // Act
        var act = () => new JsonObjectRangeTreeNode(entries, 0L, -1L);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText()
    {
        // Arrange
        var entries = CreateNumberedEntries(3);

        // Act
        var node = new JsonObjectRangeTreeNode(entries, 0L, 1000L);

        // Assert
        node.Text.Should().Be("[0 - 999]");
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText_PartialRange()
    {
        // Arrange
        var entries = CreateNumberedEntries(3);

        // Act
        var node = new JsonObjectRangeTreeNode(entries, 1000L, 500L);

        // Assert
        node.Text.Should().Be("[1,000 - 1,499]");
    }

    [Fact]
    public void Constructor_WithZeroCount_DisplayTextIsEmptyRange()
    {
        // Arrange
        var entries = CreateNumberedEntries(1);

        // Act
        var node = new JsonObjectRangeTreeNode(entries, 0L, 0L);

        // Assert
        node.Text.Should().Be("[0 - (empty)]");
    }

    // --- CreateKeyNode tests ---

    [Theory]
    [InlineData("id", "123")]
    [InlineData("name", "\"x\"")]
    [InlineData("ok", "true")]
    [InlineData("ok", "false")]
    public void CreateKeyNode_PrimitiveValue_ReturnsValueNodeWithLabel(string key, string json)
    {
        // Arrange
        var valueBytes = ToBytes(json);

        // Act
        var node = JsonObjectRangeTreeNode.CreateKeyNode(key, valueBytes);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().StartWith($"{key}: ");
    }

    [Fact]
    public void CreateKeyNode_NullValue_ReturnsValueNodeWithLabel()
    {
        // Arrange
        var valueBytes = ToBytes("null");

        // Act
        var node = JsonObjectRangeTreeNode.CreateKeyNode("k", valueBytes);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Be("k: <null>");
    }

    [Theory]
    [InlineData("k", "not-json")]
    [InlineData("k", "")]
    public void CreateKeyNode_InvalidInput_ReturnsValueNodeWithErrorText(string key, string rawValue)
    {
        // Arrange
        var valueBytes = ToBytes(rawValue);

        // Act
        var node = JsonObjectRangeTreeNode.CreateKeyNode(key, valueBytes);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Be($"{key}: [Invalid JSON]");
    }

    [Fact]
    public void CreateKeyNode_ObjectValue_ReturnsObjectNodeWithLabel()
    {
        // Arrange
        var valueBytes = ToBytes("{\"a\":1}");

        // Act
        var node = JsonObjectRangeTreeNode.CreateKeyNode("data", valueBytes);

        // Assert
        node.Should().BeOfType<JsonObjectTreeNode>();
        node.Text.Should().Be("data: {Object: 1 properties}");
    }

    [Fact]
    public void CreateKeyNode_ArrayValue_ReturnsArrayNodeWithLabel()
    {
        // Arrange
        var valueBytes = ToBytes("[1,2]");

        // Act
        var node = JsonObjectRangeTreeNode.CreateKeyNode("tags", valueBytes);

        // Assert
        node.Should().BeOfType<JsonArrayTreeNode>();
        node.Text.Should().Be("tags: [Array: 2 items]");
    }

    // --- Lazy-loading tests ---

    [Fact]
    public void IsChildrenLoaded_InitiallyFalse()
    {
        // Arrange
        var entries = CreateNumberedEntries(1);

        // Act
        var node = new JsonObjectRangeTreeNode(entries, 0L, 1L);

        // Assert
        node.IsChildrenLoaded.Should().BeFalse();
    }

    [Fact]
    public void Children_BeforeEnsureChildrenLoaded_ReturnsEmpty()
    {
        // Arrange — node has count > 0 but EnsureChildrenLoaded() is never called
        var entries = CreateNumberedEntries(3);

        // Act
        var node = new JsonObjectRangeTreeNode(entries, 0L, 3L);

        // Assert — without EnsureChildrenLoaded(), Children returns the default empty list
        node.Children.Should().BeEmpty();
    }

    [Fact]
    public void EnsureChildrenLoaded_PopulatesKeyNodes()
    {
        // Arrange
        var entries = CreateNumberedEntries(3);
        var node = new JsonObjectRangeTreeNode(entries, 0L, 3L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        var children = node.Children;
        children.Should().HaveCount(3);
        children[0].Text.Should().Be("k0: 0");
        children[2].Text.Should().Be("k2: 2");
    }

    [Fact]
    public void EnsureChildrenLoaded_KeyNodesHaveNullParentNode()
    {
        // Arrange — KeyPathBuilder walks ParentNode, so key nodes under a range node must
        // keep ParentNode null to produce the same breadcrumb as before range partitioning
        IReadOnlyList<JsonObjectEntry> entries =
        [
            new JsonObjectEntry("obj", ToBytes("{\"a\":1}")),
            new JsonObjectEntry("arr", ToBytes("[1]")),
            new JsonObjectEntry("val", ToBytes("1")),
        ];
        var node = new JsonObjectRangeTreeNode(entries, 0L, 3L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        var children = node.Children;
        children[0].Should().BeOfType<JsonObjectTreeNode>().Which.ParentNode.Should().BeNull();
        children[1].Should().BeOfType<JsonArrayTreeNode>().Which.ParentNode.Should().BeNull();
        children[2].Should().BeOfType<JsonValueTreeNode>().Which.ParentNode.Should().BeNull();
    }

    [Fact]
    public void EnsureChildrenLoaded_WhenAlreadyLoaded_IsIdempotent()
    {
        // Arrange
        var entries = CreateNumberedEntries(2);
        var node = new JsonObjectRangeTreeNode(entries, 0L, 2L);

        // Act
        node.EnsureChildrenLoaded();
        var firstChildren = node.Children;
        node.EnsureChildrenLoaded();
        var secondChildren = node.Children;

        // Assert — second call does not reload
        secondChildren.Should().BeSameAs(firstChildren);
    }

    // --- Nested range node support ---

    [Fact]
    public void EnsureChildrenLoaded_CountExceedsRangeSize_CreatesNestedRangeNodes()
    {
        // Arrange — one node covering 2500 entries exceeds RangeSize → nested ranges
        var entries = CreateNumberedEntries(2500);
        var node = new JsonObjectRangeTreeNode(entries, 0L, 2500L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        var children = node.Children;
        children.Should().HaveCount(3);
        children.Should().OnlyContain(c => c is JsonObjectRangeTreeNode);
    }

    [Fact]
    public void EnsureChildrenLoaded_CountNotMultipleOfRangeSize_LastChildHasRemainderCount()
    {
        // Arrange — 2500 count → 3 children: 1000 + 1000 + 500
        var entries = CreateNumberedEntries(2500);
        var node = new JsonObjectRangeTreeNode(entries, 0L, 2500L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        var children = node.Children;
        children.Should().HaveCount(3);
        children[0].Text.Should().Be("[0 - 999]");
        children[1].Text.Should().Be("[1,000 - 1,999]");
        children[2].Text.Should().Be("[2,000 - 2,499]");
    }

    [Fact]
    public void EnsureChildrenLoaded_NestedRangeChildrenContainCorrectKeysAndValues()
    {
        // Arrange — expand the last nested range of a 2500-entry node
        var entries = CreateNumberedEntries(2500);
        var node = new JsonObjectRangeTreeNode(entries, 0L, 2500L);

        // Act
        node.EnsureChildrenLoaded();
        var lastNested = (JsonObjectRangeTreeNode)node.Children[2];
        lastNested.EnsureChildrenLoaded();

        // Assert
        var children = lastNested.Children;
        children.Should().HaveCount(500);
        children[0].Text.Should().Be("k2000: 2000");
        children[499].Text.Should().Be("k2499: 2499");
    }
}

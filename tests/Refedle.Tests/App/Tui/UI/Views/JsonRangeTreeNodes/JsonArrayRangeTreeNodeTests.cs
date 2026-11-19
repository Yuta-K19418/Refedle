using System.Text;
using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.JsonRangeTreeNodes;
using Refedle.App.Tui.UI.Views.JsonTreeNodes;
using Refedle.Engine.IO;
using Refedle.Engine.IO.JsonArray;
using Refedle.TestHelpers.IO;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views.JsonRangeTreeNodes;

public sealed class JsonArrayRangeTreeNodeTests : IDisposable
{
    private readonly TempFile _tempFile = TempFile.ReservePath(".json");

    private readonly string _testFilePath;

    public JsonArrayRangeTreeNodeTests()
    {
        _testFilePath = _tempFile.Path;
    }

    public void Dispose() => _tempFile.Dispose();

    private RowIndexer CreateAndBuildIndexer(string jsonContent)
    {
        File.WriteAllText(_testFilePath, jsonContent);
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        return indexer;
    }

    [Fact]
    public void Constructor_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1]");
        using ElementReader reader = new(indexer.FilePath);
        IRowIndexer? nullIndexer = null;

        // Act
        Func<JsonArrayRangeTreeNode> act = () => new JsonArrayRangeTreeNode(nullIndexer!, reader, 0, 10);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullReader_ThrowsArgumentNullException()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1]");
        ElementReader? reader = null;

        // Act
        Func<JsonArrayRangeTreeNode> act = () => new JsonArrayRangeTreeNode(indexer, reader!, 0, 10);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNegativeStartIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1]");
        using ElementReader reader = new(indexer.FilePath);

        // Act
        Func<JsonArrayRangeTreeNode> act = () => new JsonArrayRangeTreeNode(indexer, reader, -1L, 10L);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1]");
        using ElementReader reader = new(indexer.FilePath);

        // Act
        Func<JsonArrayRangeTreeNode> act = () => new JsonArrayRangeTreeNode(indexer, reader, 0L, -1L);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1,2,3]");
        using ElementReader reader = new(indexer.FilePath);

        // Act
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 1000L);

        // Assert
        node.Text.Should().Be("[0 - 999]");
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText_PartialRange()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1,2,3]");
        using ElementReader reader = new(indexer.FilePath);

        // Act
        JsonArrayRangeTreeNode node = new(indexer, reader, 1000L, 500L);

        // Assert
        node.Text.Should().Be("[1,000 - 1,499]");
    }

    [Fact]
    public void EnsureChildrenLoaded_PopulatesChildren()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1, 2, 3]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 3L);

        // Act
        node.EnsureChildrenLoaded();
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(3);
    }

    [Fact]
    public void Children_EmptyRange_ReturnsEmptyChildren()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 0L);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        node.Text.Should().Be("[0 - (empty)]");
        children.Should().BeEmpty();
    }

    [Fact]
    public void EnsureChildrenLoaded_ObjectElement_CreatesJsonObjectTreeNode()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[{\"a\":1}]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 1L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        IList<ITreeNode> children = node.Children;
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>();
        children[0].Text.Should().StartWith("[0]: ");
    }

    [Fact]
    public void EnsureChildrenLoaded_ArrayElement_CreatesJsonArrayTreeNode()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[[1,2]]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 1L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        IList<ITreeNode> children = node.Children;
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>();
        children[0].Text.Should().StartWith("[0]: ");
    }

    [Fact]
    public void EnsureChildrenLoaded_PrimitiveElement_CreatesJsonValueTreeNode()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[42]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 1L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        IList<ITreeNode> children = node.Children;
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].Text.Should().Be("[0]: 42");
    }

    [Fact]
    public void EnsureChildrenLoaded_NullElement_CreatesJsonValueTreeNode()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[null, 1]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 2L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        IList<ITreeNode> children = node.Children;
        children.Should().HaveCount(2);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].Text.Should().Be("[0]: <null>");
    }

    [Fact]
    public void CreateElementNode_WithEmptyBytes_CreatesJsonValueTreeNodeWithErrorText()
    {
        // Arrange
        JsonRawBytes emptyBytes = JsonRawBytes.Empty;

        // Act
        ITreeNode elementNode = JsonArrayRangeTreeNode.CreateElementNode(emptyBytes, 0L);

        // Assert
        elementNode.Should().BeOfType<JsonValueTreeNode>();
        elementNode.Text.Should().Contain("[Invalid JSON]");
    }

    [Fact]
    public void CreateElementNode_WithMalformedJson_CreatesJsonValueTreeNodeWithErrorText()
    {
        // Arrange
        JsonRawBytes malformed = new(System.Text.Encoding.UTF8.GetBytes("{abc"));

        // Act
        ITreeNode node = JsonArrayRangeTreeNode.CreateElementNode(malformed, 5L);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Contain("[Invalid JSON]");
        node.Text.Should().StartWith("[5]: ");
    }

    [Fact]
    public void CreateElementNode_WithTruncatedObject_CreatesJsonValueTreeNodeWithErrorText()
    {
        // Arrange
        JsonRawBytes truncated = new(System.Text.Encoding.UTF8.GetBytes("{"));

        // Act
        ITreeNode node = JsonArrayRangeTreeNode.CreateElementNode(truncated, 3L);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Contain("[Invalid JSON]");
        node.Text.Should().StartWith("[3]: ");
    }

    [Fact]
    public void EnsureChildrenLoaded_CalledTwice_ReturnsSameCount()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1, 2, 3]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 3L);

        // Act
        node.EnsureChildrenLoaded();
        int firstCount = node.Children.Count;
        node.EnsureChildrenLoaded();
        int secondCount = node.Children.Count;

        // Assert
        secondCount.Should().Be(firstCount);
    }

    [Fact]
    public void EnsureChildrenLoaded_CountExceedsAvailableElements_ReturnsOnlyAvailableChildren()
    {
        // Arrange — file has 2 elements, but node requests count=3; ReadElements returns only 2
        RowIndexer indexer = CreateAndBuildIndexer("[1, 2]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 3L);

        // Act
        node.EnsureChildrenLoaded();
        IList<ITreeNode> children = node.Children;

        // Assert — only 2 elements available
        children.Should().HaveCount(2);
    }

    [Fact]
    public void Children_BeforeEnsureChildrenLoaded_ReturnsEmpty()
    {
        // Arrange — node has count > 0 but EnsureChildrenLoaded() is never called
        RowIndexer indexer = CreateAndBuildIndexer("[1, 2, 3]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 3L);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert — without EnsureChildrenLoaded(), Children returns the default empty list
        children.Should().BeEmpty();
    }

    [Fact]
    public void IsChildrenLoaded_InitiallyFalse()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 1L);

        // Act
        bool result = node.IsChildrenLoaded;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EnsureChildrenLoaded_LoadsChildren()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1, 2]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 2L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        node.IsChildrenLoaded.Should().BeTrue();
        node.Children.Should().HaveCount(2);
    }

    [Fact]
    public void EnsureChildrenLoaded_WhenAlreadyLoaded_IsIdempotent()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 1L);

        // Act
        node.EnsureChildrenLoaded();
        IList<ITreeNode> firstChildren = node.Children;
        node.EnsureChildrenLoaded();
        IList<ITreeNode> secondChildren = node.Children;

        // Assert — second call does not reload
        secondChildren.Should().BeSameAs(firstChildren);
    }

    [Fact]
    public void EnsureChildrenLoaded_EmptyRange_SetsIsChildrenLoadedTrue()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 0L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        node.IsChildrenLoaded.Should().BeTrue();
        node.Children.Should().BeEmpty();
    }

    // --- Nested range node support ---

    [Fact]
    public void LoadChildren_CountExceedsRangeSize_CreatesNestedRangeNodes()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 2500L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        IList<ITreeNode> children = node.Children;
        children.Should().HaveCount(3);
        children.Should().OnlyContain(c => c is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void LoadChildren_CountEqualsRangeSize_CreatesElementNodes()
    {
        // Arrange — 1000 elements, exactly at RangeSize boundary → element nodes, not nested range nodes
        IEnumerable<string> elements = Enumerable.Range(0, 1000)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        RowIndexer indexer = CreateAndBuildIndexer($"[{string.Join(",", elements)}]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 1000L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        IList<ITreeNode> children = node.Children;
        children.Should().HaveCount(1000);
        children.Should().NotContain(c => c is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void LoadChildren_CountNotMultipleOfRangeSize_LastChildHasRemainderCount()
    {
        // Arrange — 2500 count → 3 children: 1000 + 1000 + 500
        RowIndexer indexer = CreateAndBuildIndexer("[1]");
        using ElementReader reader = new(indexer.FilePath);
        JsonArrayRangeTreeNode node = new(indexer, reader, 0L, 2500L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        IList<ITreeNode> children = node.Children;
        children.Should().HaveCount(3);
        children[0].Text.Should().Be("[0 - 999]");
        children[1].Text.Should().Be("[1,000 - 1,999]");
        children[2].Text.Should().Be("[2,000 - 2,499]");
    }

    [Fact]
    public void Constructor_WithLongStartIndex_SetsCorrectDisplayText()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("[1]");
        using ElementReader reader = new(indexer.FilePath);

        // Act
        JsonArrayRangeTreeNode node = new(indexer, reader, 2_000_000_000L, 1000L);

        // Assert
        node.Text.Should().Be("[2,000,000,000 - 2,000,000,999]");
    }

    [Fact]
    public void CreateElementNode_ObjectToken_SetsRecordPositionAsZeroBased()
    {
        // Arrange
        JsonRawBytes bytes = new(Encoding.UTF8.GetBytes("{\"a\":1}"));

        // Act
        ITreeNode node = JsonArrayRangeTreeNode.CreateElementNode(bytes, 3L);

        // Assert
        node.Should().BeOfType<JsonObjectTreeNode>()
            .Which.RecordPosition.Should().Be(3L);
    }

    [Fact]
    public void CreateElementNode_ArrayToken_SetsRecordPositionAsZeroBased()
    {
        // Arrange
        JsonRawBytes bytes = new(Encoding.UTF8.GetBytes("[1,2]"));

        // Act
        ITreeNode node = JsonArrayRangeTreeNode.CreateElementNode(bytes, 3L);

        // Assert
        node.Should().BeOfType<JsonArrayTreeNode>()
            .Which.RecordPosition.Should().Be(3L);
    }
}

using System.Text;
using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views.JsonRangeTreeNodes;
using Refedle.App.Tui.Ui.Views.JsonTreeNodes;
using Refedle.Engine.IO;
using Refedle.Engine.IO.JsonArray;

namespace Refedle.Tests.App.Tui.Ui.Views.JsonRangeTreeNodes;

public sealed class JsonArrayRangeTreeNodeTests : IDisposable
{
    private readonly string _testFilePath;
    private bool _disposed;

    public JsonArrayRangeTreeNodeTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonarray_rangetreenode_{Guid.NewGuid()}.json"
        );
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }

            _disposed = true;
        }
    }

    private RowIndexer CreateAndBuildIndexer(string jsonContent)
    {
        File.WriteAllText(_testFilePath, jsonContent);
        var indexer = new RowIndexer(_testFilePath);
        indexer.BuildIndex();
        return indexer;
    }

    [Fact]
    public void Constructor_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[1]");
        using var reader = new ElementReader(indexer.FilePath);
        IRowIndexer? nullIndexer = null;

        // Act
        var act = () => new JsonArrayRangeTreeNode(nullIndexer!, reader, 0, 10);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullReader_ThrowsArgumentNullException()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[1]");
        ElementReader? reader = null;

        // Act
        var act = () => new JsonArrayRangeTreeNode(indexer, reader!, 0, 10);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNegativeStartIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[1]");
        using var reader = new ElementReader(indexer.FilePath);

        // Act
        var act = () => new JsonArrayRangeTreeNode(indexer, reader, -1L, 10L);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[1]");
        using var reader = new ElementReader(indexer.FilePath);

        // Act
        var act = () => new JsonArrayRangeTreeNode(indexer, reader, 0L, -1L);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[1,2,3]");
        using var reader = new ElementReader(indexer.FilePath);

        // Act
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 1000L);

        // Assert
        node.Text.Should().Be("[0 - 999]");
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText_PartialRange()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[1,2,3]");
        using var reader = new ElementReader(indexer.FilePath);

        // Act
        var node = new JsonArrayRangeTreeNode(indexer, reader, 1000L, 500L);

        // Assert
        node.Text.Should().Be("[1,000 - 1,499]");
    }

    [Fact]
    public void EnsureChildrenLoaded_PopulatesChildren()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[1, 2, 3]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 3L);

        // Act
        node.EnsureChildrenLoaded();
        var children = node.Children;

        // Assert
        children.Should().HaveCount(3);
    }

    [Fact]
    public void Children_EmptyRange_ReturnsEmptyChildren()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[1]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 0L);

        // Act
        var children = node.Children;

        // Assert
        node.Text.Should().Be("[0 - (empty)]");
        children.Should().BeEmpty();
    }

    [Fact]
    public void EnsureChildrenLoaded_ObjectElement_CreatesJsonObjectTreeNode()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[{\"a\":1}]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 1L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        var children = node.Children;
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>();
        children[0].Text.Should().StartWith("[0]: ");
    }

    [Fact]
    public void EnsureChildrenLoaded_ArrayElement_CreatesJsonArrayTreeNode()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[[1,2]]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 1L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        var children = node.Children;
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>();
        children[0].Text.Should().StartWith("[0]: ");
    }

    [Fact]
    public void EnsureChildrenLoaded_PrimitiveElement_CreatesJsonValueTreeNode()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[42]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 1L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        var children = node.Children;
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].Text.Should().Be("[0]: 42");
    }

    [Fact]
    public void EnsureChildrenLoaded_NullElement_CreatesJsonValueTreeNode()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[null, 1]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 2L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        var children = node.Children;
        children.Should().HaveCount(2);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].Text.Should().Be("[0]: <null>");
    }

    [Fact]
    public void CreateElementNode_WithEmptyBytes_CreatesJsonValueTreeNodeWithErrorText()
    {
        // Arrange
        var emptyBytes = JsonRawBytes.Empty;

        // Act
        var elementNode = JsonArrayRangeTreeNode.CreateElementNode(emptyBytes, 0L);

        // Assert
        elementNode.Should().BeOfType<JsonValueTreeNode>();
        elementNode.Text.Should().Contain("[Invalid JSON]");
    }

    [Fact]
    public void CreateElementNode_WithMalformedJson_CreatesJsonValueTreeNodeWithErrorText()
    {
        // Arrange
        var malformed = new JsonRawBytes(System.Text.Encoding.UTF8.GetBytes("{abc"));

        // Act
        var node = JsonArrayRangeTreeNode.CreateElementNode(malformed, 5L);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Contain("[Invalid JSON]");
        node.Text.Should().StartWith("[5]: ");
    }

    [Fact]
    public void CreateElementNode_WithTruncatedObject_CreatesJsonValueTreeNodeWithErrorText()
    {
        // Arrange
        var truncated = new JsonRawBytes(System.Text.Encoding.UTF8.GetBytes("{"));

        // Act
        var node = JsonArrayRangeTreeNode.CreateElementNode(truncated, 3L);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Contain("[Invalid JSON]");
        node.Text.Should().StartWith("[3]: ");
    }

    [Fact]
    public void EnsureChildrenLoaded_CalledTwice_ReturnsSameCount()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[1, 2, 3]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 3L);

        // Act
        node.EnsureChildrenLoaded();
        var firstCount = node.Children.Count;
        node.EnsureChildrenLoaded();
        var secondCount = node.Children.Count;

        // Assert
        secondCount.Should().Be(firstCount);
    }

    [Fact]
    public void EnsureChildrenLoaded_CountExceedsAvailableElements_ReturnsOnlyAvailableChildren()
    {
        // Arrange — file has 2 elements, but node requests count=3; ReadElements returns only 2
        var indexer = CreateAndBuildIndexer("[1, 2]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 3L);

        // Act
        node.EnsureChildrenLoaded();
        var children = node.Children;

        // Assert — only 2 elements available
        children.Should().HaveCount(2);
    }

    [Fact]
    public void Children_BeforeEnsureChildrenLoaded_ReturnsEmpty()
    {
        // Arrange — node has count > 0 but EnsureChildrenLoaded() is never called
        var indexer = CreateAndBuildIndexer("[1, 2, 3]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 3L);

        // Act
        var children = node.Children;

        // Assert — without EnsureChildrenLoaded(), Children returns the default empty list
        children.Should().BeEmpty();
    }

    [Fact]
    public void IsChildrenLoaded_InitiallyFalse()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[1]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 1L);

        // Act
        var result = node.IsChildrenLoaded;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EnsureChildrenLoaded_LoadsChildren()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[1, 2]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 2L);

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
        var indexer = CreateAndBuildIndexer("[1]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 1L);

        // Act
        node.EnsureChildrenLoaded();
        var firstChildren = node.Children;
        node.EnsureChildrenLoaded();
        var secondChildren = node.Children;

        // Assert — second call does not reload
        secondChildren.Should().BeSameAs(firstChildren);
    }

    [Fact]
    public void EnsureChildrenLoaded_EmptyRange_SetsIsChildrenLoadedTrue()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[1]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 0L);

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
        var indexer = CreateAndBuildIndexer("[1]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 2500L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        var children = node.Children;
        children.Should().HaveCount(3);
        children.Should().OnlyContain(c => c is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void LoadChildren_CountEqualsRangeSize_CreatesElementNodes()
    {
        // Arrange — 1000 elements, exactly at RangeSize boundary → element nodes, not nested range nodes
        var elements = Enumerable.Range(0, 1000)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var indexer = CreateAndBuildIndexer($"[{string.Join(",", elements)}]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 1000L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        var children = node.Children;
        children.Should().HaveCount(1000);
        children.Should().NotContain(c => c is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void LoadChildren_CountNotMultipleOfRangeSize_LastChildHasRemainderCount()
    {
        // Arrange — 2500 count → 3 children: 1000 + 1000 + 500
        var indexer = CreateAndBuildIndexer("[1]");
        using var reader = new ElementReader(indexer.FilePath);
        var node = new JsonArrayRangeTreeNode(indexer, reader, 0L, 2500L);

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
    public void Constructor_WithLongStartIndex_SetsCorrectDisplayText()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("[1]");
        using var reader = new ElementReader(indexer.FilePath);

        // Act
        var node = new JsonArrayRangeTreeNode(indexer, reader, 2_000_000_000L, 1000L);

        // Assert
        node.Text.Should().Be("[2,000,000,000 - 2,000,000,999]");
    }

    [Fact]
    public void CreateElementNode_ObjectToken_SetsRecordPositionAsZeroBased()
    {
        // Arrange
        var bytes = new JsonRawBytes(Encoding.UTF8.GetBytes("{\"a\":1}"));

        // Act
        var node = JsonArrayRangeTreeNode.CreateElementNode(bytes, 3L);

        // Assert
        node.Should().BeOfType<JsonObjectTreeNode>()
            .Which.RecordPosition.Should().Be(3L);
    }

    [Fact]
    public void CreateElementNode_ArrayToken_SetsRecordPositionAsZeroBased()
    {
        // Arrange
        var bytes = new JsonRawBytes(Encoding.UTF8.GetBytes("[1,2]"));

        // Act
        var node = JsonArrayRangeTreeNode.CreateElementNode(bytes, 3L);

        // Assert
        node.Should().BeOfType<JsonArrayTreeNode>()
            .Which.RecordPosition.Should().Be(3L);
    }
}

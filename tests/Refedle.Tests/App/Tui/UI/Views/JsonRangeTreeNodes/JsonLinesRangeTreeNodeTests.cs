using System.Text;
using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.JsonRangeTreeNodes;
using Refedle.App.Tui.UI.Views.JsonTreeNodes;
using Refedle.Engine.IO;
using Refedle.Engine.IO.JsonLines;
using Refedle.TestHelpers.IO;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views.JsonRangeTreeNodes;

public sealed class JsonLinesRangeTreeNodeTests : IDisposable
{
    private readonly TempFile _tempFile = TempFile.ReservePath(".jsonl");

    private readonly string _testFilePath;

    public JsonLinesRangeTreeNodeTests()
    {
        _testFilePath = _tempFile.Path;
    }

    public void Dispose() => _tempFile.Dispose();

    private RowIndexer CreateAndBuildIndexer(string content)
    {
        File.WriteAllText(_testFilePath, content);
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        return indexer;
    }

    [Fact]
    public void Constructor_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}");
        using RowReader reader = new(indexer.FilePath);
        IRowIndexer? nullIndexer = null;

        // Act
        Func<JsonLinesRangeTreeNode> act = () => new JsonLinesRangeTreeNode(nullIndexer!, reader, 0, 10);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullReader_ThrowsArgumentNullException()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}");
        RowReader? reader = null;

        // Act
        Func<JsonLinesRangeTreeNode> act = () => new JsonLinesRangeTreeNode(indexer, reader!, 0, 10);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNegativeStartIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}");
        using RowReader reader = new(indexer.FilePath);

        // Act
        Func<JsonLinesRangeTreeNode> act = () => new JsonLinesRangeTreeNode(indexer, reader, -1L, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}");
        using RowReader reader = new(indexer.FilePath);

        // Act
        Func<JsonLinesRangeTreeNode> act = () => new JsonLinesRangeTreeNode(indexer, reader, 0L, -1L);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}");
        using RowReader reader = new(indexer.FilePath);

        // Act
        JsonLinesRangeTreeNode node = new(indexer, reader, 0L, 1000L);

        // Assert
        node.Text.Should().Be("Lines 1 - 1,000");
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText_PartialRange()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}");
        using RowReader reader = new(indexer.FilePath);

        // Act
        JsonLinesRangeTreeNode node = new(indexer, reader, 1000L, 500L);

        // Assert
        node.Text.Should().Be("Lines 1,001 - 1,500");
    }

    [Fact]
    public void EnsureChildrenLoaded_PopulatesChildren()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}\n{\"b\":2}\n{\"c\":3}");
        using RowReader reader = new(indexer.FilePath);
        JsonLinesRangeTreeNode node = new(indexer, reader, 0L, 3L);

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
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}");
        using RowReader reader = new(indexer.FilePath);
        JsonLinesRangeTreeNode node = new(indexer, reader, 0L, 0L);

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        node.Text.Should().Be("Lines 1 (empty)");
        children.Should().BeEmpty();
    }

    [Fact]
    public void EnsureChildrenLoaded_CountExceedsAvailableLines_ReturnsOnlyAvailableChildren()
    {
        // Arrange — file has 2 lines, but node requests count=3; ReadLines returns only 2
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}\n{\"b\":2}");
        using RowReader reader = new(indexer.FilePath);
        JsonLinesRangeTreeNode node = new(indexer, reader, 0L, 3L);

        // Act
        node.EnsureChildrenLoaded();
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().HaveCount(2);
    }

    [Fact]
    public void CreateLineNode_WithEmptyBytes_CreatesInvalidNode()
    {
        // Arrange
        JsonRawBytes emptyBytes = JsonRawBytes.Empty;

        // Act
        ITreeNode node = JsonLinesRangeTreeNode.CreateLineNode(emptyBytes, 0L);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Contain("[Invalid JSON]");
    }

    [Fact]
    public void CreateLineNode_WithMalformedJson_CreatesInvalidNode()
    {
        // Arrange
        JsonRawBytes malformed = new("{abc"u8.ToArray());

        // Act
        ITreeNode node = JsonLinesRangeTreeNode.CreateLineNode(malformed, 5L);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Contain("[Invalid JSON]");
        node.Text.Should().StartWith("Line 6:");
    }

    [Theory]
    [InlineData("{\"a\":1}", typeof(JsonObjectTreeNode), "Line 1: {Object: 1 properties}")]
    [InlineData("[1,2]", typeof(JsonArrayTreeNode), "Line 1: [Array: 2 items]")]
    [InlineData("42", typeof(JsonValueTreeNode), "Line 1: 42")]
    public void CreateLineNode_ByTokenType_CreatesCorrectNodeAndLabel(
        string json, Type expectedType, string expectedLabel)
    {
        // Arrange
        JsonRawBytes bytes = new(System.Text.Encoding.UTF8.GetBytes(json));

        // Act
        ITreeNode node = JsonLinesRangeTreeNode.CreateLineNode(bytes, 0L);

        // Assert
        node.Should().BeOfType(expectedType);
        node.Text.Should().Be(expectedLabel);
    }

    [Fact]
    public void IsChildrenLoaded_InitiallyFalse()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}");
        using RowReader reader = new(indexer.FilePath);
        JsonLinesRangeTreeNode node = new(indexer, reader, 0L, 1L);

        // Act
        bool result = node.IsChildrenLoaded;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EnsureChildrenLoaded_LoadsChildren()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}\n{\"b\":2}");
        using RowReader reader = new(indexer.FilePath);
        JsonLinesRangeTreeNode node = new(indexer, reader, 0L, 2L);

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
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}");
        using RowReader reader = new(indexer.FilePath);
        JsonLinesRangeTreeNode node = new(indexer, reader, 0L, 1L);

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
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}");
        using RowReader reader = new(indexer.FilePath);
        JsonLinesRangeTreeNode node = new(indexer, reader, 0L, 0L);

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
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}");
        using RowReader reader = new(indexer.FilePath);
        JsonLinesRangeTreeNode node = new(indexer, reader, 0L, 2500L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        IList<ITreeNode> children = node.Children;
        children.Should().HaveCount(3);
        children.Should().OnlyContain(c => c is JsonLinesRangeTreeNode);
    }

    [Fact]
    public void LoadChildren_CountEqualsRangeSize_CreatesLineNodes()
    {
        // Arrange — 1000 lines, exactly at RangeSize boundary → line nodes, not nested range nodes
        IEnumerable<string> lines = Enumerable.Range(0, 1000).Select(i => $"{{\"id\":{i}}}");
        RowIndexer indexer = CreateAndBuildIndexer(string.Join("\n", lines));
        using RowReader reader = new(indexer.FilePath);
        JsonLinesRangeTreeNode node = new(indexer, reader, 0L, 1000L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        IList<ITreeNode> children = node.Children;
        children.Should().HaveCount(1000);
        children.Should().NotContain(c => c is JsonLinesRangeTreeNode);
    }

    [Fact]
    public void LoadChildren_CountNotMultipleOfRangeSize_LastChildHasRemainderCount()
    {
        // Arrange — 2500 count → 3 children: 1000 + 1000 + 500
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}");
        using RowReader reader = new(indexer.FilePath);
        JsonLinesRangeTreeNode node = new(indexer, reader, 0L, 2500L);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        IList<ITreeNode> children = node.Children;
        children.Should().HaveCount(3);
        children[0].Text.Should().Be("Lines 1 - 1,000");
        children[1].Text.Should().Be("Lines 1,001 - 2,000");
        children[2].Text.Should().Be("Lines 2,001 - 2,500");
    }

    [Fact]
    public void Constructor_WithLongStartIndex_SetsCorrectDisplayText()
    {
        // Arrange
        RowIndexer indexer = CreateAndBuildIndexer("{\"a\":1}");
        using RowReader reader = new(indexer.FilePath);

        // Act
        JsonLinesRangeTreeNode node = new(indexer, reader, 2_000_000_000L, 1000L);

        // Assert
        node.Text.Should().Be("Lines 2,000,000,001 - 2,000,001,000");
    }

    [Fact]
    public void CreateLineNode_WithNewlineOnlyBytes_CreatesInvalidNode()
    {
        // Arrange — a bare newline (blank line content) is not valid JSON
        JsonRawBytes bytes = new("\n"u8.ToArray());

        // Act
        ITreeNode node = JsonLinesRangeTreeNode.CreateLineNode(bytes, 0L);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Contain("[Invalid JSON]");
    }

    [Fact]
    public void CreateLineNode_WithUtf8Bom_CreatesInvalidNode()
    {
        // Arrange — BOM (EF BB BF) prefix is not a valid JSON token
        JsonRawBytes bytes = new([0xEF, 0xBB, 0xBF, .. "{\"a\":1}"u8]);

        // Act
        ITreeNode node = JsonLinesRangeTreeNode.CreateLineNode(bytes, 0L);

        // Assert — reader cannot parse the BOM-prefixed bytes, so the line is treated as invalid
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().StartWith("Line 1:");
        node.Text.Should().Contain("[Invalid JSON]");
    }

    [Fact]
    public void CreateLineNode_ObjectToken_SetsRecordPositionAsOneBased()
    {
        // Arrange
        JsonRawBytes bytes = new(Encoding.UTF8.GetBytes("{\"a\":1}"));

        // Act
        ITreeNode node = JsonLinesRangeTreeNode.CreateLineNode(bytes, 3L);

        // Assert
        node.Should().BeOfType<JsonObjectTreeNode>()
            .Which.RecordPosition.Should().Be(4L);
    }

    [Fact]
    public void CreateLineNode_ArrayToken_SetsRecordPositionAsOneBased()
    {
        // Arrange
        JsonRawBytes bytes = new(Encoding.UTF8.GetBytes("[1,2]"));

        // Act
        ITreeNode node = JsonLinesRangeTreeNode.CreateLineNode(bytes, 3L);

        // Assert
        node.Should().BeOfType<JsonArrayTreeNode>()
            .Which.RecordPosition.Should().Be(4L);
    }
}

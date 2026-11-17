using System.Text;
using AwesomeAssertions;
using Refedle.Engine.IO.JsonArray;

namespace Refedle.Tests.Engine.IO.JsonArray;

public sealed partial class RowIndexerTests
{
    [Fact]
    public void GetCheckPoint_WithUtf8BomAndLaterCheckpoint_ReadsExpectedElement()
    {
        // Arrange — 1,500 elements force a second checkpoint; record 1000 must stay readable
        // at its absolute post-BOM offset.
        IEnumerable<string> elements = Enumerable.Range(0, 1500).Select(i => $"{{\"id\":{i}}}");
        string body = $"[{string.Join(",", elements)}]";
        File.WriteAllBytes(_testFilePath, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(body)]);
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        string marker = "{\"id\":1000}";
        long expectedOffset = (long)body.IndexOf(marker, StringComparison.Ordinal) + 3; // + BOM bytes

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(1000);

        // Assert
        indexer.TotalRows.Should().Be(1500);
        rowOffset.Should().Be(0);
        byteOffset.Should().Be(expectedOffset);
        using ElementReader reader = new(_testFilePath);
        IReadOnlyList<JsonRawBytes> elementsAtCheckpoint = reader.ReadElements(byteOffset, rowOffset, 1);
        elementsAtCheckpoint.Should().HaveCount(1);
        Encoding.UTF8.GetString(elementsAtCheckpoint[0].Span).Should().Be(marker);
    }

    [Fact]
    public void GetCheckPoint_BeforeBuildIndex_ReturnsNegativeOne()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[{\"a\":1}]");
        RowIndexer indexer = new(_testFilePath);

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(0);

        // Assert
        byteOffset.Should().Be(-1);
        rowOffset.Should().Be(0);
    }

    [Fact]
    public void GetCheckPoint_Element0_ReturnsCorrectOffset()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[{\"a\":1}]");
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(0);

        // Assert
        byteOffset.Should().Be(1); // offset of '{', after '['
        rowOffset.Should().Be(0);
    }

    [Fact]
    public void GetCheckPoint_Element500_ReturnsCheckpoint0WithRowOffset500()
    {
        // Arrange
        IEnumerable<string> elements = Enumerable.Range(0, 800).Select(i => $"{{\"id\":{i}}}");
        File.WriteAllText(_testFilePath, $"[{string.Join(",", elements)}]");
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(500);

        // Assert
        byteOffset.Should().Be(1); // checkpoint 0 = offset of first element
        rowOffset.Should().Be(500);
    }

    [Fact]
    public void GetCheckPoint_Element1000_ReturnsCheckpoint1WithZeroRowOffset()
    {
        // Arrange
        IEnumerable<string> elements = Enumerable.Range(0, 1500).Select(i => $"{{\"id\":{i}}}");
        string content = $"[{string.Join(",", elements)}]";
        File.WriteAllText(_testFilePath, content);
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        string marker = "{\"id\":1000}";
        long expectedOffset = content.IndexOf(marker, StringComparison.Ordinal);

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(1000);

        // Assert
        rowOffset.Should().Be(0);
        byteOffset.Should().Be(expectedOffset);
    }

    [Fact]
    public async Task GetCheckPoint_WhileIndexingInBackground_IsThreadSafe()
    {
        // Arrange
        IEnumerable<string> elements = Enumerable.Range(0, 2500).Select(i => $"{{\"id\":{i}}}");
        File.WriteAllText(_testFilePath, $"[{string.Join(",", elements)}]");
        RowIndexer indexer = new(_testFilePath);
        bool overlap = false;
        indexer.FirstCheckpointReached += () =>
        {
            _ = indexer.GetCheckPoint(0);
            overlap = true;
        };

        // Act: start indexing in background and call GetCheckPoint from the event callback
        Task buildTask = Task.Run(() => indexer.BuildIndex());

        // Assert: no exception thrown during concurrent access
        await buildTask;
        overlap.Should().BeTrue();
    }

    [Fact]
    public void GetCheckPoint_BeyondTotalRows_ClampsToLastCheckpoint()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[{\"id\":1},{\"id\":2},{\"id\":3}]");
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act — targetRow 5000 / 1000 = idealIndex 5, but only 1 checkpoint exists → clamped to 0
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(5000);

        // Assert
        byteOffset.Should().Be(1); // clamped to checkpoint 0
        rowOffset.Should().Be(5000);
    }

    [Fact]
    public void GetCheckPoint_WithLeadingWhitespace_ReturnsCorrectByteOffset()
    {
        // Arrange — 2-byte leading whitespace before '['
        File.WriteAllText(_testFilePath, "  [{\"a\":1}]");
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(0);

        // Assert — offset of '{' is 3 (2 whitespace + '[' = byte 3)
        byteOffset.Should().Be(3);
        rowOffset.Should().Be(0);
    }

    [Fact]
    public void GetCheckPoint_WithNegativeTargetRow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[{\"id\":1},{\"id\":2},{\"id\":3}]");
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        Func<(long byteOffset, int rowOffset)> act = () => indexer.GetCheckPoint(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

}

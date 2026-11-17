using AwesomeAssertions;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed partial class RowIndexerTests
{
    [Fact]
    public void GetCheckPoint_BeforeBuildIndex_ReturnsZero()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"id\": 1}");
        RowIndexer indexer = new(_testFilePath);

        // Act
        (long byteOffset, int rowOffset) result = indexer.GetCheckPoint(0);

        // Assert
        result.byteOffset.Should().Be(0); // _checkpoints is initialized with [0]
        result.rowOffset.Should().Be(0);
    }

    [Fact]
    public void GetCheckPoint_AfterBuildIndex_ReturnsCorrectCheckpoints()
    {
        // Arrange
        string content = "{\"id\": 1}\n{\"id\": 2}\n{\"id\": 3}";
        File.WriteAllText(_testFilePath, content);
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act & Assert
        (long byteOffset0, int rowOffset0) = indexer.GetCheckPoint(0);
        byteOffset0.Should().Be(0); // First checkpoint is at start of file
        rowOffset0.Should().Be(0);

        (long byteOffset1, int rowOffset1) = indexer.GetCheckPoint(1);
        byteOffset1.Should().Be(0);
        rowOffset1.Should().Be(1);

        (long byteOffset2, int rowOffset2) = indexer.GetCheckPoint(2);
        byteOffset2.Should().Be(0);
        rowOffset2.Should().Be(2);
    }

    [Fact]
    public void GetCheckPoint_WithCheckpointInterval_ReturnsCorrectOffsets()
    {
        // Arrange: Create file with more than CheckPointInterval (1000) rows
        IEnumerable<string> lines = Enumerable.Range(0, 1500).Select(i => $"{{\"id\": {i}}}");
        string content = string.Join("\n", lines);
        File.WriteAllText(_testFilePath, content);
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act & Assert
        (long byteOffset500, int rowOffset500) = indexer.GetCheckPoint(500);
        byteOffset500.Should().Be(0); // Before first checkpoint
        rowOffset500.Should().Be(500);

        (long byteOffset999, int rowOffset999) = indexer.GetCheckPoint(999);
        byteOffset999.Should().Be(0);
        rowOffset999.Should().Be(999);

        (long byteOffset1000, int rowOffset1000) = indexer.GetCheckPoint(1000);
        byteOffset1000.Should().Be(11890); // Checkpoint should be at 1000th row boundary
        rowOffset1000.Should().Be(0);

        (long byteOffset1499, int rowOffset1499) = indexer.GetCheckPoint(1499);
        byteOffset1499.Should().Be(11890); // Checkpoint at 1000 rows
        rowOffset1499.Should().Be(499); // 1499 - 1000 = 499 rows from checkpoint
    }

    [Fact]
    public void GetCheckPoint_RequestBeyondTotalRows_ClampsToLastCheckpoint()
    {
        // Arrange
        string content = "{\"id\": 1}\n{\"id\": 2}\n{\"id\": 3}";
        File.WriteAllText(_testFilePath, content);
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(100);

        // Assert
        byteOffset.Should().Be(0); // Clamped to last checkpoint
        rowOffset.Should().Be(100); // But rowOffset is still 100 (will be handled by caller)
    }

    [Fact]
    public void GetCheckPoint_WithEmptyFile_ReturnsZero()
    {
        // Arrange
        File.WriteAllText(_testFilePath, string.Empty);
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(0);

        // Assert
        byteOffset.Should().Be(0); // _checkpoints is initialized with [0]
        rowOffset.Should().Be(0);
    }

    [Fact]
    public void GetCheckPoint_WithSingleRow_ReturnsCorrectOffset()
    {
        // Arrange
        string content = "{\"id\": 1}";
        File.WriteAllText(_testFilePath, content);
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(0);

        // Assert
        byteOffset.Should().Be(0);
        rowOffset.Should().Be(0);
    }

    [Fact]
    public async Task GetCheckPoint_WhileIndexingInBackground_ShouldBeThreadSafe()
    {
        // Arrange: Create a large file (2500 rows) to test concurrent access during indexing
        IEnumerable<string> lines = Enumerable.Range(0, 2500).Select(i => $"{{\"id\": {i}}}");
        string content = string.Join("\n", lines);
        File.WriteAllText(_testFilePath, content);
        RowIndexer indexer = new(_testFilePath);

        // Act: Start indexing in background and access checkpoints concurrently
        Task buildTask = Task.Run(() => indexer.BuildIndex());

        // Assert: Checkpoints should be accessible even during indexing
        (long byteOffset0, int rowOffset0) = indexer.GetCheckPoint(0);
        (long byteOffset500, int rowOffset500) = indexer.GetCheckPoint(500);
        // During indexing, checkpoint may not be available yet
        // But method should not crash and return valid values

        // Wait for indexing to complete
        await buildTask;

        // Assert
        byteOffset0.Should().Be(0);
        rowOffset0.Should().Be(0);
        byteOffset500.Should().Be(0);
        rowOffset500.Should().Be(500);
        // After indexing, checkpoints should be properly set
        (long finalByteOffset1000, int finalRowOffset1000) = indexer.GetCheckPoint(1000);
        finalByteOffset1000.Should().Be(11890);
        finalRowOffset1000.Should().Be(0);
    }

    [Fact]
    public void GetCheckPoint_WithExactCheckpointRow_ReturnsZeroRowOffset()
    {
        // Arrange: Create file with checkpoint interval rows + 1 to have exact checkpoint row
        IEnumerable<string> lines = Enumerable.Range(0, 1001).Select(i => $"{{\"id\": {i}}}");
        string content = string.Join("\n", lines);
        File.WriteAllText(_testFilePath, content);
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(1000);

        // Assert
        byteOffset.Should().Be(11890); // Checkpoint should be at 1000th row boundary
        rowOffset.Should().Be(0);
    }

    [Fact]
    public void GetCheckPoint_WithNegativeTargetRow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"id\": 1}\n{\"id\": 2}\n{\"id\": 3}");
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        Func<(long byteOffset, int rowOffset)> act = () => indexer.GetCheckPoint(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetCheckPoint_WithLargeFile_ReturnsValidCheckpoints()
    {
        // Arrange: Create file with 10,000 rows to ensure multiple checkpoints
        IEnumerable<string> lines = Enumerable.Range(0, 10_000).Select(i => $"{{\"id\": {i}}}");
        string content = string.Join("\n", lines);
        File.WriteAllText(_testFilePath, content);
        RowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Test specific positions
        (long byteOffset0, int rowOffset0) = indexer.GetCheckPoint(0);
        byteOffset0.Should().Be(0);
        rowOffset0.Should().Be(0);

        (long byteOffset999, int rowOffset999) = indexer.GetCheckPoint(999);
        byteOffset999.Should().Be(0);
        rowOffset999.Should().Be(999);

        (long byteOffset1000, int rowOffset1000) = indexer.GetCheckPoint(1000);
        byteOffset1000.Should().Be(11890); // Checkpoint at 1000 rows
        rowOffset1000.Should().Be(0);

        (long byteOffset9999, int rowOffset9999) = indexer.GetCheckPoint(9999);
        byteOffset9999.Should().Be(115890); // Checkpoint at 9000 rows
        rowOffset9999.Should().Be(999); // 999 rows from checkpoint
    }
}

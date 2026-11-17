using AwesomeAssertions;
using Refedle.Engine.IO.Csv;

namespace Refedle.Tests.Engine.IO.Csv;

public sealed partial class DataRowIndexerTests
{
    [Fact]
    public void GetCheckPoint_WithRowLessThanCheckpoint_ReturnsFirstCheckpoint()
    {
        // Arrange: Fixed-length rows "v001,d001\n" = 10 bytes each
        // Header "col1,col2\n" = 10 bytes
        List<string> lines = ["col1,col2"];
        lines.AddRange(Enumerable.Range(1, 500).Select(i => $"v{i:D3},d{i:D3}"));
        File.WriteAllText(_testFilePath, string.Join("\n", lines) + "\n");

        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(250);

        // Assert
        byteOffset.Should().Be(10); // First checkpoint after header (10 bytes)
        rowOffset.Should().Be(250);
    }

    [Fact]
    public void GetCheckPoint_WithExactCheckpointRow_ReturnsExactCheckpoint()
    {
        // Arrange: Fixed-length rows "v0001,d0001\n" = 12 bytes each
        // Header "col01,col02\n" = 12 bytes
        List<string> lines = ["col01,col02"];
        lines.AddRange(Enumerable.Range(1, 1500).Select(i => $"v{i:D4},d{i:D4}"));
        File.WriteAllText(_testFilePath, string.Join("\n", lines) + "\n");

        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(1000);

        // Assert
        // First checkpoint after header: 12 bytes
        // Checkpoint 1000: After header + 1000 data rows = 12 + (1000 * 12) = 12 + 12000 = 12012 bytes
        byteOffset.Should().Be(12012);
        rowOffset.Should().Be(0);
    }

    [Fact]
    public void GetCheckPoint_WithRowBeyondLastCheckpoint_ReturnsLastCheckpoint()
    {
        // Arrange: Fixed-length rows "v0001,d0001\n" = 12 bytes each
        // Header "col01,col02\n" = 12 bytes
        List<string> lines = ["col01,col02"];
        lines.AddRange(Enumerable.Range(1, 1200).Select(i => $"v{i:D4},d{i:D4}"));
        File.WriteAllText(_testFilePath, string.Join("\n", lines) + "\n");

        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(1150);

        // Assert
        // First checkpoint after header: 12 bytes
        // Checkpoint 1000: After header + 1000 data rows = 12 + (1000 * 12) = 12 + 12000 = 12012 bytes
        // Row 1150 uses the same checkpoint 1000 (since there are only 1200 data rows)
        byteOffset.Should().Be(12012);
        rowOffset.Should().Be(150);
    }

    [Fact]
    public void GetCheckPoint_WithRowBeyondTotalRows_ReturnsLastCheckpointWithCorrectOffset()
    {
        // Arrange: File with 2000 data rows (checkpoints at 0, 1000, 2000)
        // Fixed-length rows "v0001,d0001\n" = 12 bytes each
        // Header "col01,col02\n" = 12 bytes
        List<string> lines = ["col01,col02"];
        lines.AddRange(Enumerable.Range(1, 2000).Select(i => $"v{i:D4},d{i:D4}"));
        File.WriteAllText(_testFilePath, string.Join("\n", lines) + "\n");

        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Verify TotalRows is 2000 (header excluded, only data rows)
        indexer.TotalRows.Should().Be(2000);

        // Act: Request row 5000, which is far beyond TotalRows
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(5000);

        // Assert: Returns last checkpoint (2000)
        // idealCheckPointIndex = 5000 / 1000 = 5
        // actualCheckPointIndex = Math.Min(5, 2) = 2 (checkpoints: header, 1000, 2000)
        // actualCheckPointRow = 2 * 1000 = 2000
        // rowOffset = 5000 - 2000 = 3000
        // Checkpoint 2000: After header + 2000 data rows = 12 + (2000 * 12) = 12 + 24000 = 24012 bytes
        byteOffset.Should().Be(24012); // Last checkpoint at row 2000
        rowOffset.Should().Be(3000); // Need to skip 3000 rows from checkpoint 2000
    }

    [Fact]
    public void GetCheckPoint_WithNegativeRow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2\nval3,val4");
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        Func<(long byteOffset, int rowOffset)> act = () => indexer.GetCheckPoint(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetCheckPoint_WithRowZero_ReturnsFirstCheckpoint()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2\nval3,val4");
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(0);

        // Assert
        // Header length: "col1,col2\n" = 10 bytes
        byteOffset.Should().Be(10); // First checkpoint after header
        rowOffset.Should().Be(0);  // No offset needed
    }

    [Fact]
    public void GetCheckPoint_WithExactlyCheckpointInterval_ReturnsCorrectCheckpoint()
    {
        // Arrange: Create file with 2000 rows
        List<string> lines = ["col1,col2"];
        lines.AddRange(Enumerable.Range(1, 2000).Select(i => $"v{i:D4},d{i:D4}"));
        File.WriteAllText(_testFilePath, string.Join("\n", lines));

        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();

        // Act: Request row 1000 (exactly checkpoint interval)
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(1000);

        // Assert
        // Row 1000 should be at checkpoint 1000
        rowOffset.Should().Be(0);
        // byteOffset should be positive (after header and 1000 rows)
        byteOffset.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetCheckPoint_BeforeBuildIndex_ReturnsZeroCheckpoint()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2\nval3,val4");
        DataRowIndexer indexer = new(_testFilePath);

        // Act (before BuildIndex)
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(100);

        // Assert: Should return -1 when no checkpoints exist (before BuildIndex)
        byteOffset.Should().Be(-1);
        rowOffset.Should().Be(0);
    }

    [Fact]
    public async Task GetCheckPoint_ThreadSafety_ConcurrentAccessDoesNotThrow()
    {
        // Arrange
        List<string> lines = ["col1,col2"];
        lines.AddRange(Enumerable.Range(1, 5000).Select(i => $"v{i:D4},d{i:D4}"));
        File.WriteAllText(_testFilePath, string.Join("\n", lines));

        DataRowIndexer indexer = new(_testFilePath);

        // Act: Start indexing in background
        Task indexingTask = Task.Run(() => indexer.BuildIndex());

        // Concurrently call GetCheckPoint from multiple threads
        List<Task> tasks = [];
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    (long byteOffset, int rowOffset) = indexer.GetCheckPoint(j * 50);
                    // Should not throw
                    _ = byteOffset;
                    _ = rowOffset;
                }
            }));
        }

        // Wait for all tasks
        await Task.WhenAll([.. tasks, indexingTask]);

        // Assert: No exceptions should have been thrown
        indexer.TotalRows.Should().Be(5000);
    }
}

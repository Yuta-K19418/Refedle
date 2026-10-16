using AwesomeAssertions;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed partial class RowIndexerTests
{
    [Fact]
    public void BuildIndex_WithMultipleJsonObjectsLF_IndexesAllRows()
    {
        // Arrange
        var content = """
            {"id": 1, "name": "Alice"}
            {"id": 2, "name": "Bob"}
            {"id": 3, "name": "Charlie"}
            """;
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithMultipleJsonObjectsCRLF_IndexesAllRows()
    {
        // Arrange
        var content =
            "{\"id\": 1, \"name\": \"Alice\"}\r\n{\"id\": 2, \"name\": \"Bob\"}\r\n{\"id\": 3, \"name\": \"Charlie\"}";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithMixedLineEndings_IndexesAllRows()
    {
        // Arrange
        var content = "{\"id\": 1}\n{\"id\": 2}\r\n{\"id\": 3}";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithTrailingNewline_IndexesCorrectly()
    {
        // Arrange
        var content = "{\"id\": 1}\n{\"id\": 2}\n";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // 2 data rows (trailing newline doesn't create a new row)
    }

    [Fact]
    public void BuildIndex_WithoutTrailingNewline_CountsLastRow()
    {
        // Arrange
        var content = "{\"id\": 1}\n{\"id\": 2}\n{\"id\": 3}";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithComplexJson_IndexesCorrectly()
    {
        // Arrange
        var content = """
            {"id": 1, "nested": {"name": "Alice"}, "array": [1, 2, 3]}
            {"id": 2, "nested": {"name": "Bob"}, "array": [4, 5, 6]}
            {"id": 3, "nested": {"name": "Charlie"}, "array": [7, 8, 9]}
            """;
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithJsonContainingNewlineEscapes_HandlesCorrectly()
    {
        // Arrange
        var content =
            "{\"text\": \"line1\\\\nline2\\\\nline3\"}\n{\"text\": \"another\\\\nline\"}\n{\"text\": \"escaped\\\\nstring\"}";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithUnicodeContent_IndexesCorrectly()
    {
        // Arrange
        var content = """
            {"name": "日本語", "id": 1}
            {"name": "한국어", "id": 2}
            {"name": "हिन्दी", "id": 3}
            """;
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithEmptyLines_CountsEmptyRows()
    {
        // Arrange
        var content = "\n\n{\"id\": 1}\n\n\n{\"id\": 2}\n";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(6); // 2 empty + 1 data + 3 empty + 1 data
    }

    [Fact]
    public void BuildIndex_WithWhitespaceOnlyLines_CountsAsRows()
    {
        // Arrange
        var content = "   \n\t\n{\"id\": 1}\n  \n";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(4); // 2 whitespace + 1 data + 1 whitespace
    }

    [Fact]
    public void BuildIndex_WithEmptyFile_SetsTotalRowsToZero()
    {
        // Arrange
        File.WriteAllText(_testFilePath, string.Empty);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(0);
    }

    [Fact]
    public void BuildIndex_WithSingleJsonObject_ReturnsOneRow()
    {
        // Arrange
        var content = "{\"id\": 1, \"name\": \"Alice\"}";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(1);
    }

    [Fact]
    public void BuildIndex_WithLargeFile_ProcessesCorrectly()
    {
        // Arrange: Create file with 10,000 lines (enough to trigger checkpointing)
        var lines = Enumerable
            .Range(0, 10_000)
            .Select(i => $"{{\"id\": {i}, \"name\": \"User{i}\"}}");
        var content = string.Join("\n", lines);
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(10_000);
    }

    [Fact]
    public void BuildIndex_RaisesFirstCheckpointReached_OnFirstThousandRows()
    {
        // Arrange
        var lines = Enumerable.Range(0, 1_500).Select(i => $"{{\"id\": {i}}}");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new RowIndexer(_testFilePath);
        var firstCheckpointFired = false;
        indexer.FirstCheckpointReached += () => firstCheckpointFired = true;

        // Act
        indexer.BuildIndex();

        // Assert
        firstCheckpointFired.Should().BeTrue();
    }

    [Fact]
    public void BuildIndex_FirstCheckpointReached_FiresOnlyOnce()
    {
        // Arrange
        var lines = Enumerable.Range(0, 3_000).Select(i => $"{{\"id\": {i}}}");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new RowIndexer(_testFilePath);
        var fireCount = 0;
        indexer.FirstCheckpointReached += () => Interlocked.Increment(ref fireCount);

        // Act
        indexer.BuildIndex();

        // Assert
        fireCount.Should().Be(1);
    }

    [Fact]
    public void BuildIndex_RaisesFirstCheckpointReached_WhenFileIsEmpty()
    {
        // Arrange
        File.WriteAllText(_testFilePath, string.Empty);
        var indexer = new RowIndexer(_testFilePath);
        var firstCheckpointFired = false;
        indexer.FirstCheckpointReached += () => firstCheckpointFired = true;

        // Act
        indexer.BuildIndex();

        // Assert
        firstCheckpointFired.Should().BeTrue();
    }

    [Fact]
    public async Task BuildIndex_RaisesFirstCheckpointReached_WhenCancelledBeforeFirstCheckpoint()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        // Just enough data to start, but we will cancel immediately
        var lines = Enumerable.Range(0, 10).Select(i => $"{{\"id\": {i}}}");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new RowIndexer(_testFilePath);
        var firstCheckpointFired = false;
        indexer.FirstCheckpointReached += () => firstCheckpointFired = true;

        // Act: Cancel before even starting the loop to ensure it hits the cancellation check early
        cts.Cancel();
        var task = Task.Run(() => indexer.BuildIndex(cts.Token));

        // Assert
        Func<Task> act = () => task;
        await act.Should().ThrowAsync<OperationCanceledException>();
        firstCheckpointFired.Should().BeTrue(); // Should fire from finally block
    }

    [Fact]
    public void BuildIndex_WithMultipleCheckpoints_RaisesProgressChangedAtLeastOnce()
    {
        // Arrange
        var lines = Enumerable.Range(0, 2_500).Select(i => $"{{\"id\": {i}}}");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new RowIndexer(_testFilePath);
        var progressCount = 0;
        indexer.ProgressChanged += (_, _) => Interlocked.Increment(ref progressCount);

        // Act
        indexer.BuildIndex();

        // Assert
        progressCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void BuildIndex_RaisesBuildIndexCompleted_AfterCompletion()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"id\": 1}\n{\"id\": 2}");
        var indexer = new RowIndexer(_testFilePath);
        var buildIndexCompletedFired = false;
        indexer.BuildIndexCompleted += () => buildIndexCompletedFired = true;

        // Act
        indexer.BuildIndex();

        // Assert
        buildIndexCompletedFired.Should().BeTrue();
    }

    [Fact]
    public async Task BuildIndex_RaisesBuildIndexCompleted_WhenCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        // 2000 rows ensures at least one ProgressChanged event at 1000 rows
        var lines = Enumerable.Range(0, 2_000).Select(i => $"{{\"id\": {i}}}");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new RowIndexer(_testFilePath);
        var buildIndexCompletedFired = false;
        indexer.BuildIndexCompleted += () => buildIndexCompletedFired = true;

        // Cancel when the first progress event is reached
        indexer.ProgressChanged += (_, _) => cts.Cancel();

        // Act
        var task = Task.Run(() => indexer.BuildIndex(cts.Token));

        // Assert
        Func<Task> act = () => task;
        await act.Should().ThrowAsync<OperationCanceledException>();
        buildIndexCompletedFired.Should().BeTrue();
    }

    [Fact]
    public void BuildIndex_RaisesBuildIndexCompleted_WhenFileIsEmpty()
    {
        // Arrange
        File.WriteAllText(_testFilePath, string.Empty);
        var indexer = new RowIndexer(_testFilePath);
        var buildIndexCompletedFired = false;
        indexer.BuildIndexCompleted += () => buildIndexCompletedFired = true;

        // Act
        indexer.BuildIndex();

        // Assert
        buildIndexCompletedFired.Should().BeTrue();
    }

    [Fact]
    public async Task BuildIndex_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var lines = Enumerable.Range(0, 2_000).Select(i => $"{{\"id\": {i}}}");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new RowIndexer(_testFilePath);

        // Cancel when progress is made
        indexer.ProgressChanged += (_, _) => cts.Cancel();

        // Act
        var task = Task.Run(() => indexer.BuildIndex(cts.Token));

        // Assert
        Func<Task> act = () => task;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void BytesRead_IncreasesMonotonically_DuringBuildIndex()
    {
        // Arrange
        var lines = Enumerable.Range(0, 300_000).Select(i => $"{{\"id\": {i}}}");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new RowIndexer(_testFilePath);
        List<long> progressEvents = [];
        indexer.ProgressChanged += (bytesRead, _) => progressEvents.Add(bytesRead);

        // Act
        indexer.BuildIndex();

        // Assert
        progressEvents.Should().HaveCountGreaterThanOrEqualTo(2);
        progressEvents.Should().BeInAscendingOrder();
        indexer.BytesRead.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FileSize_MatchesActualFileLength()
    {
        // Arrange
        var content = "{\"id\":1}\n{\"id\":2}\n{\"id\":3}";
        File.WriteAllText(_testFilePath, content);
        var expectedFileSize = new FileInfo(_testFilePath).Length;
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.FileSize.Should().Be(expectedFileSize);
    }

    [Fact]
    public void BuildIndex_WithMalformedLine_LiteralNewlineCountedAsRowSeparator()
    {
        // Arrange
        // First line has unclosed quote with literal newline, second line is valid
        var content = "{\"text\":\"malformed line\n{\"id\":2,\"name\":\"Bob\"}\n";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        // Literal newline should be counted as row separator even with unclosed quote
        indexer.TotalRows.Should().Be(2);
    }
}

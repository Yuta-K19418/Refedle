using AwesomeAssertions;
using Refedle.Engine.IO.JsonArray;

namespace Refedle.Tests.Engine.IO.JsonArray;

public sealed partial class RowIndexerTests
{
    [Fact]
    public void BuildIndex_WithEmptyArray_ReturnsZeroTotalRows()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[]");
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(0);
    }

    [Fact]
    public void BuildIndex_WithEmptyArray_FiresBuildIndexCompleted()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[]");
        var indexer = new RowIndexer(_testFilePath);
        var buildIndexCompletedFired = false;
        indexer.BuildIndexCompleted += () => buildIndexCompletedFired = true;

        // Act
        indexer.BuildIndex();

        // Assert
        buildIndexCompletedFired.Should().BeTrue();
    }

    [Fact]
    public void BuildIndex_WithSinglePrimitive_ReturnsOneElement()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[42]");
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(1);
    }

    [Fact]
    public void BuildIndex_WithSingleObject_ReturnsOneElement()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[{\"a\":1}]");
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(1);
    }

    [Fact]
    public void BuildIndex_WithLeadingWhitespace_ReturnsCorrectTotalRows()
    {
        // Arrange — 2-byte leading whitespace before '['
        File.WriteAllText(_testFilePath, "  [{\"a\":1}]");
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(1);
    }

    [Fact]
    public void BuildIndex_WithMixedTypes_IndexesAllElements()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[{}, [], 1, \"s\", null, true]");
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(6);
    }

    [Fact]
    public void BuildIndex_WithDeeplyNestedObject_IndexesCorrectly()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[{\"a\":{\"b\":{\"c\":1}}}]");
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(1);
    }

    [Fact]
    public void BuildIndex_With1001Elements_IndexesAllElements()
    {
        // Arrange
        var elements = Enumerable.Range(0, 1001).Select(i => $"{{\"id\":{i}}}");
        File.WriteAllText(_testFilePath, $"[{string.Join(",", elements)}]");
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(1001);
    }

    [Fact]
    public void BuildIndex_With1001Elements_FirstCheckpointReachedFiresOnce()
    {
        // Arrange
        var elements = Enumerable.Range(0, 1001).Select(i => $"{{\"id\":{i}}}");
        File.WriteAllText(_testFilePath, $"[{string.Join(",", elements)}]");
        var indexer = new RowIndexer(_testFilePath);
        var fireCount = 0;
        indexer.FirstCheckpointReached += () => Interlocked.Increment(ref fireCount);

        // Act
        indexer.BuildIndex();

        // Assert
        fireCount.Should().Be(1);
    }

    [Fact]
    public void BuildIndex_StructuredElementSpansBuffer_IndexesCorrectly()
    {
        // Arrange — object with many small fields so the total exceeds 1 MB
        // without any single JSON string token exceeding the buffer
        var fields = string.Join(",", Enumerable.Range(0, 90_000).Select(i => $"\"f{i}\":{i}"));
        File.WriteAllText(_testFilePath, $"[{{{fields}}}]");
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(1);
    }

    [Fact]
    public void BuildIndex_StringValueExceedsBufferSize_ThrowsNotSupportedException()
    {
        // Arrange — string value larger than 1 MB buffer
        var oversizedString = new string('a', 1024 * 1024 + 1);
        File.WriteAllText(_testFilePath, $"[\"{oversizedString}\"]");
        var indexer = new RowIndexer(_testFilePath);

        // Act
        var act = () => indexer.BuildIndex();

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task BuildIndex_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        // Use enough elements to exceed 1 MB buffer to ensure multiple outer loop iterations
        var elements = Enumerable.Range(0, 100_000).Select(i => $"{{\"id\":{i}}}");
        File.WriteAllText(_testFilePath, $"[{string.Join(",", elements)}]");
        var indexer = new RowIndexer(_testFilePath);
        indexer.ProgressChanged += (_, _) => cts.Cancel();

        // Act
        var task = Task.Run(() => indexer.BuildIndex(cts.Token));

        // Assert
        Func<Task> act = () => task;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BuildIndex_WhenCancelled_BuildIndexCompletedStillFires()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var elements = Enumerable.Range(0, 100_000).Select(i => $"{{\"id\":{i}}}");
        File.WriteAllText(_testFilePath, $"[{string.Join(",", elements)}]");
        var indexer = new RowIndexer(_testFilePath);
        var buildIndexCompletedFired = false;
        indexer.BuildIndexCompleted += () => buildIndexCompletedFired = true;
        indexer.ProgressChanged += (_, _) => cts.Cancel();

        // Act
        var task = Task.Run(() => indexer.BuildIndex(cts.Token));

        // Assert
        Func<Task> act = () => task;
        await act.Should().ThrowAsync<OperationCanceledException>();
        buildIndexCompletedFired.Should().BeTrue();
    }

    [Fact]
    public void BuildIndex_LessThanCheckpointInterval_FiresFirstCheckpointReached()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[{\"a\":1},{\"b\":2},{\"c\":3}]");
        var indexer = new RowIndexer(_testFilePath);
        var firstCheckpointFired = false;
        indexer.FirstCheckpointReached += () => firstCheckpointFired = true;

        // Act
        indexer.BuildIndex();

        // Assert
        firstCheckpointFired.Should().BeTrue();
    }

    [Fact]
    public void BuildIndex_RaisesFirstCheckpointReached_WithEmptyArray()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[]");
        var indexer = new RowIndexer(_testFilePath);
        var firstCheckpointFired = false;
        indexer.FirstCheckpointReached += () => firstCheckpointFired = true;

        // Act
        indexer.BuildIndex();

        // Assert
        firstCheckpointFired.Should().BeTrue();
    }

    [Fact]
    public async Task BuildIndex_RaisesFirstCheckpointReached_WhenCancelledBeforeCheckpoint()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var elements = Enumerable.Range(0, 10).Select(i => $"{{\"id\":{i}}}");
        File.WriteAllText(_testFilePath, $"[{string.Join(",", elements)}]");
        var indexer = new RowIndexer(_testFilePath);
        var firstCheckpointFired = false;
        indexer.FirstCheckpointReached += () => firstCheckpointFired = true;
        cts.Cancel();

        // Act
        var task = Task.Run(() => indexer.BuildIndex(cts.Token));

        // Assert
        Func<Task> act = () => task;
        await act.Should().ThrowAsync<OperationCanceledException>();
        firstCheckpointFired.Should().BeTrue();
    }

    [Fact]
    public void BuildIndex_WithMultipleCheckpoints_RaisesProgressChangedAtLeastOnce()
    {
        // Arrange
        var elements = Enumerable.Range(0, 2500).Select(i => $"{{\"id\":{i}}}");
        File.WriteAllText(_testFilePath, $"[{string.Join(",", elements)}]");
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
        File.WriteAllText(_testFilePath, "[{\"id\":1},{\"id\":2}]");
        var indexer = new RowIndexer(_testFilePath);
        var buildIndexCompletedFired = false;
        indexer.BuildIndexCompleted += () => buildIndexCompletedFired = true;

        // Act
        indexer.BuildIndex();

        // Assert
        buildIndexCompletedFired.Should().BeTrue();
    }

    [Fact]
    public void BuildIndex_BytesRead_IncreasesMonotonically()
    {
        // Arrange
        var elements = Enumerable.Range(0, 300_000).Select(i => $"{{\"id\":{i}}}");
        File.WriteAllText(_testFilePath, $"[{string.Join(",", elements)}]");
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
    public void BuildIndex_FileSize_MatchesActualFileLength()
    {
        // Arrange
        var content = "[{\"id\":1},{\"id\":2},{\"id\":3}]";
        File.WriteAllText(_testFilePath, content);
        var expectedFileSize = new FileInfo(_testFilePath).Length;
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.FileSize.Should().Be(expectedFileSize);
    }
}

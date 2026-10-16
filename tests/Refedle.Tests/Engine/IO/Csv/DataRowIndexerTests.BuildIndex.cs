using AwesomeAssertions;
using Refedle.Engine.IO.Csv;

namespace Refedle.Tests.Engine.IO.Csv;

public sealed partial class DataRowIndexerTests
{
    [Fact]
    public void BuildIndex_WithSimpleCsv_IndexesAllRows()
    {
        // Arrange
        var content = "header1,header2,header3\nvalue1,value2,value3\nvalue4,value5,value6";
        File.WriteAllText(_testFilePath, content);
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithQuotedFieldContainingComma_HandlesCorrectly()
    {
        // Arrange
        var content =
            "name,description,price\n\"Smith, John\",\"A product, with comma\",100\n\"Doe, Jane\",Normal,200";
        File.WriteAllText(_testFilePath, content);
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithQuotedFieldContainingNewline_HandlesCorrectly()
    {
        // Arrange
        var content =
            "name,description\n\"John\",\"Line1\nLine2\nLine3\"\n\"Jane\",\"Single line\"";
        File.WriteAllText(_testFilePath, content);
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithEscapedQuotes_HandlesCorrectly()
    {
        // Arrange (RFC 4180: quotes are escaped as "")
        var content =
            "name,quote\n\"John\",\"He said \"\"Hello\"\"\"\n\"Jane\",\"She said \"\"Hi\"\"\"";
        File.WriteAllText(_testFilePath, content);
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithCRLF_HandlesCorrectly()
    {
        // Arrange
        var content = "header1,header2\r\nvalue1,value2\r\nvalue3,value4";
        File.WriteAllText(_testFilePath, content);
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithMixedLineEndings_HandlesCorrectly()
    {
        // Arrange
        var content = "header1,header2\nvalue1,value2\r\nvalue3,value4\nvalue5,value6";
        File.WriteAllText(_testFilePath, content);
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithEmptyFile_ReturnsZeroRows()
    {
        // Arrange
        File.WriteAllText(_testFilePath, string.Empty);
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(0);
    }

    [Fact]
    public void BuildIndex_WithHeaderOnly_ReturnsZeroRows()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "header1,header2,header3");
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(0); // Header is excluded, no data rows
    }

    [Fact]
    public void BuildIndex_WithTrailingNewline_IndexesCorrectly()
    {
        // Arrange
        var content = "header1,header2\nvalue1,value2\nvalue3,value4\n";
        File.WriteAllText(_testFilePath, content);
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, empty line after trailing newline is ignored (not a data row)
    }

    [Fact]
    public void BuildIndex_WithComplexQuotedFields_HandlesCorrectly()
    {
        // Arrange: Mix of quoted and unquoted fields, commas and newlines inside quotes
        var content =
            "name,address,notes\n\"Smith, John\",\"123 Main St\nApt 4\",\"Has a cat\"\n\"Doe, Jane\",\"456 \"\"Oak\"\" Avenue\",\"Likes \"\"pizza\"\"\"\nNormal,Simple,Data";

        File.WriteAllText(_testFilePath, content);
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithUnicodeContent_IndexesCorrectly()
    {
        // Arrange
        var content = "名前,説明\n太郎,\"日本語, テスト\"\n花子,シンプル";
        File.WriteAllText(_testFilePath, content);
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithOnlyNewlines_IndexesCorrectly()
    {
        // Arrange
        var content = "\n\n\n";
        File.WriteAllText(_testFilePath, content);
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header line is empty line, so 2 data rows (empty lines) remain
    }

    [Fact]
    public void BuildIndex_WithQuotedEmptyField_HandlesCorrectly()
    {
        // Arrange
        var content = "col1,col2,col3\nval1,\"\",val3\nval4,\"\",val6";
        File.WriteAllText(_testFilePath, content);
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithVeryLargeFile_HandlesCorrectly()
    {
        // Arrange: Create file with exactly 1001 rows to test checkpoint boundary
        var lines = Enumerable.Range(1, 1001).Select(i => $"value{i:D4},data{i:D4}").Prepend("col1,col2");
        File.WriteAllText(_testFilePath, string.Join("\n", lines));

        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(1001);
    }

    [Fact]
    public void BuildIndex_WithPartialQuotesAtBufferBoundary_HandlesCorrectly()
    {
        // Arrange: Create content where quote spans buffer boundary
        var longValue = new string('a', 1024 * 1024); // 1MB value
        var content = $"col1,col2\n\"{longValue}\",normal";
        File.WriteAllText(_testFilePath, content);

        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(1);
    }

    [Fact]
    public void BuildIndex_RaisesFirstCheckpointReached_OnFirstThousandRows()
    {
        // Arrange
        var lines = Enumerable.Range(1, 1_500).Select(i => $"value{i:D4},data{i:D4}").Prepend("col1,col2");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new DataRowIndexer(_testFilePath);
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
        var lines = Enumerable.Range(1, 3_000).Select(i => $"value{i:D4},data{i:D4}").Prepend("col1,col2");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new DataRowIndexer(_testFilePath);
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
        File.WriteAllText(_testFilePath, "col1,col2");
        var indexer = new DataRowIndexer(_testFilePath);
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
        var lines = Enumerable.Range(1, 10).Select(i => $"v,d").Prepend("c1,c2");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new DataRowIndexer(_testFilePath);
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
        var lines = Enumerable.Range(1, 2_500).Select(i => $"value{i:D4},data{i:D4}").Prepend("col1,col2");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new DataRowIndexer(_testFilePath);
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
        string[] lines = ["col1,col2", "value1,data1", "value2,data2"];
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new DataRowIndexer(_testFilePath);
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
        var lines = Enumerable.Range(1, 2_000).Select(i => $"v,d").Prepend("c1,c2");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new DataRowIndexer(_testFilePath);
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
        File.WriteAllText(_testFilePath, "col1,col2");
        var indexer = new DataRowIndexer(_testFilePath);
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
        var lines = Enumerable.Range(1, 2_000).Select(i => $"v,d").Prepend("c1,c2");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new DataRowIndexer(_testFilePath);

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
        var lines = Enumerable.Range(1, 200_000).Select(i => $"value{i:D6},data{i:D6}").Prepend("col1,col2");
        File.WriteAllLines(_testFilePath, lines);
        var indexer = new DataRowIndexer(_testFilePath);
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
        string[] lines = ["col1,col2", "value1,data1", "value2,data2", "value3,data3"];
        File.WriteAllLines(_testFilePath, lines);
        var expectedFileSize = new FileInfo(_testFilePath).Length;
        var indexer = new DataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.FileSize.Should().Be(expectedFileSize);
    }
}

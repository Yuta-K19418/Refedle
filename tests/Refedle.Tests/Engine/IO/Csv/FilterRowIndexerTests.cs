using AwesomeAssertions;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.Tests.Engine.Filtering;

namespace Refedle.Tests.Engine.IO.Csv;

public sealed class FilterRowIndexerTests : IDisposable
{
    private readonly string _testFilePath =
        Path.Combine(Path.GetTempPath(), $"csvFilterRowIndexer_{Guid.NewGuid()}.csv");

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public async Task BuildIndexAsync_WithConsistentColumnCounts_IndexesMatchingRows()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "name,age\nalice,1\nbob,2\nalice,3");
        var filterIndexer = CreateFilterIndexer();

        // Act
        await filterIndexer.BuildIndexAsync(CancellationToken.None);

        // Assert
        filterIndexer.TotalMatchedRows.Should().Be(2);
        filterIndexer.GetSourceRow(1).Should().Be(2);
    }

    [Theory]
    [InlineData("name,age\nalice,1\nbob\nalice,3")]
    [InlineData("name,age\nalice,1\nbob,2,x\nalice,3")]
    [InlineData("name,age\nalice\nbob")]
    public async Task BuildIndexAsync_WithMismatchedColumnCount_ThrowsInvalidDataException(string csvContent)
    {
        // Arrange
        File.WriteAllText(_testFilePath, csvContent);
        var filterIndexer = CreateFilterIndexer();

        // Act
        var act = async () => await filterIndexer.BuildIndexAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task BuildIndexAsync_WithSingleBatch_FiresProgressChangedOnceWithFinalCounts()
    {
        // Arrange
        var (filterIndexer, events) = await CreateFilterIndexerAsync(["a", "b"]);

        // Act
        await filterIndexer.BuildIndexAsync(CancellationToken.None);

        // Assert — the single batch is the final batch, so it always notifies with the full counts
        events.Should().ContainSingle().Which.Should().Be((2, 2));
        filterIndexer.TotalMatchedRows.Should().Be(1);
        filterIndexer.GetSourceRow(0).Should().Be(0);
    }

    [Fact]
    public async Task BuildIndexAsync_WithMultipleBatches_ProgressReportsEveryBatchBoundary()
    {
        // Arrange
        var (filterIndexer, events) = await CreateFilterIndexerAsync(
            [.. Enumerable.Range(0, 2500).Select(static _ => "a")]);

        // Act
        await filterIndexer.BuildIndexAsync(CancellationToken.None);

        // Assert — each batch advances the scan by more than 1% of the total, so all of them notify
        events.Should().Equal((1000, 2500), (2000, 2500), (2500, 2500));
        filterIndexer.TotalMatchedRows.Should().Be(2500);
        filterIndexer.GetSourceRow(2499).Should().Be(2499);
    }

    [Fact]
    public async Task BuildIndexAsync_WithManyRows_SkipsBatchesBelowOnePercentAndReportsShortFinalBatch()
    {
        // Arrange — each 1000-row batch is below 1% of 150001 rows, so some batches are skipped
        var (filterIndexer, events) = await CreateFilterIndexerAsync(
            [.. Enumerable.Range(0, 150_001).Select(static _ => "a")]);

        // Act
        await filterIndexer.BuildIndexAsync(CancellationToken.None);

        // Assert
        events.Should().NotContain((1_000, 150_001));
        events.Should().Contain((2_000, 150_001));
        events.Should().NotContain((3_000, 150_001));
        events.Should().Contain((4_000, 150_001));
        events[^2].Should().Be((150_000, 150_001));
        events[^1].Should().Be((150_001, 150_001));
    }

    [Fact]
    public async Task BuildIndexAsync_WithEmptySource_CompletesWithoutProgress()
    {
        // Arrange — a header-only file has no data rows to scan
        var (filterIndexer, events) = await CreateFilterIndexerAsync([]);

        // Act
        await filterIndexer.BuildIndexAsync(CancellationToken.None);

        // Assert — no rows means no batch, so no progress notification fires
        events.Should().BeEmpty();
        filterIndexer.TotalMatchedRows.Should().Be(0);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(1000)]
    [InlineData(1001)]
    public async Task BuildIndexAsync_AtBatchBoundary_ReportsFinalCountsAndMatchesEveryRow(int rowCount)
    {
        // Arrange
        var (filterIndexer, events) = await CreateFilterIndexerAsync([.. Enumerable.Range(0, rowCount).Select(static _ => "a")]);

        // Act
        await filterIndexer.BuildIndexAsync(CancellationToken.None);

        // Assert
        events[^1].Should().Be((rowCount, rowCount));
        filterIndexer.TotalMatchedRows.Should().Be(rowCount);
        filterIndexer.GetSourceRow(rowCount - 1).Should().Be(rowCount - 1);
    }

    [Fact]
    public async Task BuildIndexAsync_WhenCanceledAfterFirstProgress_ThrowsWithoutFurtherProgress()
    {
        // Arrange
        var (filterIndexer, events) = await CreateFilterIndexerAsync([.. Enumerable.Range(0, 2500).Select(static _ => "a")]);
        using var cts = new CancellationTokenSource();
        filterIndexer.ProgressChanged += (_, _) => cts.Cancel();

        // Act
        var act = async () => await filterIndexer.BuildIndexAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        events.Should().Equal((1000, 2500));
    }

    [Fact]
    public async Task BuildIndexAsync_WhenCanceledWhileYielded_ThrowsWithoutReportingProgress()
    {
        // Arrange — a single batch is the final one, which would always notify if not guarded
        var (filterIndexer, events) = await CreateFilterIndexerAsync([.. Enumerable.Range(0, 2).Select(static _ => "a")]);
        using var cts = new CancellationTokenSource();
        var context = new YieldCapturingSynchronizationContext();
        var scan = context.Start(() => filterIndexer.BuildIndexAsync(cts.Token));

        // Act — cancel while the scan is suspended at the yield, then resume it
        cts.Cancel();
        context.RunNext();
        var act = async () => await scan;

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        events.Should().BeEmpty();
    }

    private FilterRowIndexer CreateFilterIndexer()
    {
        var rowIndexer = new DataRowIndexer(_testFilePath);
        rowIndexer.BuildIndex();
        FilterSpec spec = new(0, ColumnType.Text, FilterOperator.Equals, "alice");
        return new FilterRowIndexer(rowIndexer, sourceColumnCount: 2, [spec]);
    }

    private async Task<(FilterRowIndexer FilterIndexer, List<(int scannedRows, int totalRows)> Events)>
        CreateFilterIndexerAsync(IReadOnlyList<string> dataRows)
    {
        await File.WriteAllLinesAsync(_testFilePath, ["col1", .. dataRows]);
        var rowIndexer = new DataRowIndexer(_testFilePath);
        rowIndexer.BuildIndex();
        var filterIndexer = new FilterRowIndexer(rowIndexer, sourceColumnCount: 1, [MakeSpec("a")]);
        List<(int scannedRows, int totalRows)> events = [];
        filterIndexer.ProgressChanged += (scannedRows, totalRows) => events.Add((scannedRows, totalRows));
        return (filterIndexer, events);
    }

    private static FilterSpec MakeSpec(string value) =>
        new(0, ColumnType.Text, FilterOperator.Equals, value);
}

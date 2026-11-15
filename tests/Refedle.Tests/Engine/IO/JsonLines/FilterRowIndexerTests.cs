using AwesomeAssertions;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.Tests.Engine.Filtering;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed class FilterRowIndexerTests : IDisposable
{
    private readonly string _testFilePath;

    public FilterRowIndexerTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonlinesFilterRowIndexer_{Guid.NewGuid()}.jsonl"
        );
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public async Task BuildIndexAsync_WholeNumberFilterGreaterThan_ExcludesRawExponentAndDecimalNotation()
    {
        // Arrange
        FilterSpec spec = new(0, ColumnType.WholeNumber, FilterOperator.GreaterThan, "999");

        // Act
        var filterIndexer = await BuildFilterIndexAsync(
            ["{\"value\": 1e3}", "{\"value\": 1000}", "{\"value\": 1.0}"],
            spec
        );

        // Assert — raw "1e3" and "1.0" fail long parsing; only the plain "1000" row matches
        filterIndexer.TotalMatchedRows.Should().Be(1);
        filterIndexer.GetSourceRow(0).Should().Be(1);
    }

    [Fact]
    public async Task BuildIndexAsync_WholeNumberFilterGreaterThanOrEqual_ExcludesRawDecimalNotation()
    {
        // Arrange
        FilterSpec spec = new(0, ColumnType.WholeNumber, FilterOperator.GreaterThanOrEqual, "1");

        // Act
        var filterIndexer = await BuildFilterIndexAsync(
            ["{\"value\": 1.0}", "{\"value\": 1}"],
            spec
        );

        // Assert — raw "1.0" fails long parsing even though it is integral; only "1" matches
        filterIndexer.TotalMatchedRows.Should().Be(1);
        filterIndexer.GetSourceRow(0).Should().Be(1);
    }

    [Fact]
    public async Task BuildIndexAsync_TextEqualsFilter_MatchesRawTokenText()
    {
        // Arrange
        FilterSpec spec = new(0, ColumnType.Text, FilterOperator.Equals, "1.50");

        // Act
        var filterIndexer = await BuildFilterIndexAsync(["{\"value\": 1.50}"], spec);

        // Assert — string operators compare against the raw token text
        filterIndexer.TotalMatchedRows.Should().Be(1);
    }

    [Fact]
    public async Task BuildIndexAsync_TextEqualsFilter_NormalizedText_DoesNotMatchRawToken()
    {
        // Arrange
        FilterSpec spec = new(0, ColumnType.Text, FilterOperator.Equals, "1.5");

        // Act
        var filterIndexer = await BuildFilterIndexAsync(["{\"value\": 1.50}"], spec);

        // Assert — the normalized spelling no longer matches the raw "1.50" token
        filterIndexer.TotalMatchedRows.Should().Be(0);
    }

    [Fact]
    public async Task BuildIndexAsync_FloatingPointFilter_ComparesRawTokensNumerically()
    {
        // Arrange
        FilterSpec spec = new(0, ColumnType.FloatingPoint, FilterOperator.GreaterThan, "1.4");

        // Act
        var filterIndexer = await BuildFilterIndexAsync(
            ["{\"value\": 1.50}", "{\"value\": 1.4}", "{\"value\": 1e3}"],
            spec
        );

        // Assert — "1.50" and "1e3" still compare numerically; the boundary "1.4" is excluded
        filterIndexer.TotalMatchedRows.Should().Be(2);
        filterIndexer.GetSourceRow(0).Should().Be(0);
        filterIndexer.GetSourceRow(1).Should().Be(2);
    }

    private async Task<FilterRowIndexer> BuildFilterIndexAsync(
        IReadOnlyList<string> lines,
        FilterSpec spec
    )
    {
        await File.WriteAllLinesAsync(_testFilePath, lines);
        var rowIndexer = new RowIndexer(_testFilePath);
        rowIndexer.BuildIndex();
        var filterIndexer = new FilterRowIndexer(
            rowIndexer,
            _testFilePath,
            ["value"u8.ToArray()],
            [spec]
        );
        await filterIndexer.BuildIndexAsync(CancellationToken.None);
        return filterIndexer;
    }

    private async Task<(FilterRowIndexer FilterIndexer, List<(int scannedRows, int totalRows)> Events)>
        CreateFilterIndexerAsync(IReadOnlyList<string> lines, FilterSpec spec)
    {
        await File.WriteAllLinesAsync(_testFilePath, lines);
        var rowIndexer = new RowIndexer(_testFilePath);
        rowIndexer.BuildIndex();
        var filterIndexer = new FilterRowIndexer(
            rowIndexer,
            _testFilePath,
            ["value"u8.ToArray()],
            [spec]
        );
        List<(int scannedRows, int totalRows)> events = [];
        filterIndexer.ProgressChanged += (scannedRows, totalRows) => events.Add((scannedRows, totalRows));
        return (filterIndexer, events);
    }

    private Task<(FilterRowIndexer FilterIndexer, List<(int scannedRows, int totalRows)> Events)>
        CreateMatchingFilterIndexerAsync(int rowCount) =>
        CreateFilterIndexerAsync(
            [.. Enumerable.Range(0, rowCount).Select(static _ => "{\"value\": 1}")],
            new FilterSpec(0, ColumnType.Text, FilterOperator.Equals, "1")
        );

    [Fact]
    public async Task BuildIndexAsync_WithSingleBatch_FiresProgressChangedOnceWithFinalCounts()
    {
        // Arrange
        FilterSpec spec = new(0, ColumnType.Text, FilterOperator.Equals, "1.50");
        var (filterIndexer, events) =
            await CreateFilterIndexerAsync(["{\"value\": 1.50}", "{\"value\": 1.4}"], spec);

        // Act
        await filterIndexer.BuildIndexAsync(CancellationToken.None);

        // Assert — the single batch is the final batch, so it always notifies with the full counts
        events.Should().ContainSingle().Which.Should().Be((2, 2));
    }

    [Fact]
    public async Task BuildIndexAsync_WithMultipleBatches_ProgressReportsEveryBatchBoundary()
    {
        // Arrange
        FilterSpec spec = new(0, ColumnType.Text, FilterOperator.Equals, "1");
        var lines = Enumerable.Range(0, 2500).Select(static _ => "{\"value\": 1}").ToArray();
        var (filterIndexer, events) = await CreateFilterIndexerAsync(lines, spec);

        // Act
        await filterIndexer.BuildIndexAsync(CancellationToken.None);

        // Assert — each batch advances the scan by more than 1% of the total, so all of them notify
        events.Should().Equal((1000, 2500), (2000, 2500), (2500, 2500));
        filterIndexer.TotalMatchedRows.Should().Be(2500);
    }

    [Fact]
    public async Task BuildIndexAsync_WithManyRows_SkipsBatchesBelowOnePercentAndReportsShortFinalBatch()
    {
        // Arrange — each 1000-row batch is below 1% of 150001 rows, so some batches are skipped
        var (filterIndexer, events) = await CreateMatchingFilterIndexerAsync(150_001);

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
        // Arrange
        FilterSpec spec = new(0, ColumnType.Text, FilterOperator.Equals, "1");
        var (filterIndexer, events) = await CreateFilterIndexerAsync([], spec);

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
        var (filterIndexer, events) = await CreateMatchingFilterIndexerAsync(rowCount);

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
        var (filterIndexer, events) = await CreateMatchingFilterIndexerAsync(2500);
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
        var (filterIndexer, events) = await CreateMatchingFilterIndexerAsync(2);
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
}

using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed class FocusedTableFilterRowIndexerTests
{
    private sealed class FakeFocusedSource(string[][] data) : IExtendedTableSource
    {
        public long TotalRows => data.Length;
        public int Columns => data.Length > 0 ? data[0].Length : 0;
        public string[] ColumnNames => [];
        public string[] RawColumnNames => [];
        public object this[long row, int col] => data[(int)row][col];
    }

    /// <summary>
    /// Source whose read of a configured row blocks until released, so tests can cancel
    /// the scan while a batch is in flight without sleeping.
    /// </summary>
    private sealed class GatedFocusedSource(string[][] data, int gateRowIndex) : IExtendedTableSource, IDisposable
    {
        private readonly ManualResetEventSlim _gate = new(false);
        private long _readCount;

        public TaskCompletionSource GateReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public long TotalRows => data.Length;
        public int Columns => data[0].Length;
        public string[] ColumnNames => [];
        public string[] RawColumnNames => [];
        public long ReadCount => Interlocked.Read(ref _readCount);

        public object this[long row, int col]
        {
            get
            {
                Interlocked.Increment(ref _readCount);
                if (row == gateRowIndex)
                {
                    GateReached.TrySetResult();
                    _gate.Wait();
                }

                return data[(int)row][col];
            }
        }

        public void ReleaseGate() => _gate.Set();

        public void Dispose() => _gate.Dispose();
    }

    private static FilterSpec MakeSpec(int sourceColumnIndex, string value) =>
        new(sourceColumnIndex, ColumnType.Text, FilterOperator.Equals, value);

    /// <summary>
    /// Joins a gated scan's worker with a bounded wait: WhenAny observes a canceled or
    /// faulted worker without throwing, while a worker stuck past the bound fails the test.
    /// </summary>
    private static async Task JoinScanWorkerAsync(Task build)
    {
        await Task.WhenAny(build.WaitAsync(TimeSpan.FromSeconds(10)));
        build.IsCompleted.Should().BeTrue("a scan worker must terminate within the bounded cleanup wait");
    }

    /// <summary>
    /// Source whose read of a configured row throws, so tests can fault the scan at a
    /// chosen row deterministically.
    /// </summary>
    private sealed class FaultingFocusedSource(string[][] data, int faultRowIndex) : IExtendedTableSource
    {
        public long TotalRows => data.Length;
        public int Columns => data[0].Length;
        public string[] ColumnNames => [];
        public string[] RawColumnNames => [];

        public object this[long row, int col] =>
            row == faultRowIndex ? throw new InvalidOperationException("row read failed") : data[(int)row][col];
    }

    /// <summary>Threshold used by tests that do not target the threshold wait itself.</summary>
    private const int RevealThreshold = 200;

    [Fact]
    public void Constructor_WithNullSource_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FocusedTableFilterRowIndexer(null!, [], RevealThreshold);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullFilterSpecs_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FocusedTableFilterRowIndexer(new FakeFocusedSource([]), null!, RevealThreshold);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Constructor_WithNonPositiveRevealThreshold_ThrowsArgumentOutOfRangeException(int revealThreshold)
    {
        // Act
        var act = () => new FocusedTableFilterRowIndexer(new FakeFocusedSource([]), [], revealThreshold);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task BuildIndexAsync_MatchesOnlyRowsSatisfyingFilter()
    {
        // Arrange
        var source = new FakeFocusedSource([["[0]", "A"], ["[1]", "B"], ["[2]", "A"]]);
        var indexer = new FocusedTableFilterRowIndexer(source, [MakeSpec(0, "A")], RevealThreshold);

        // Act
        await indexer.BuildIndexAsync(CancellationToken.None);

        // Assert
        indexer.TotalMatchedRows.Should().Be(2);
        indexer.GetSourceRow(0).Should().Be(0);
        indexer.GetSourceRow(1).Should().Be(2);
    }

    [Fact]
    public async Task BuildIndexAsync_WithMultipleSpecs_UsesAndSemantics()
    {
        // Arrange
        var source = new FakeFocusedSource([["[0]", "A", "x"], ["[1]", "A", "y"], ["[2]", "B", "y"]]);
        var indexer = new FocusedTableFilterRowIndexer(source, [MakeSpec(0, "A"), MakeSpec(1, "y")], RevealThreshold);

        // Act
        await indexer.BuildIndexAsync(CancellationToken.None);

        // Assert
        indexer.TotalMatchedRows.Should().Be(1);
        indexer.GetSourceRow(0).Should().Be(1);
    }

    [Fact]
    public async Task BuildIndexAsync_WithMoreRowsThanOneBatch_ScansAllRows()
    {
        // Arrange
        var rows = Enumerable.Range(0, 2500).Select(i => new[] { $"[{i}]", i % 2 == 0 ? "A" : "B" }).ToArray();
        var indexer = new FocusedTableFilterRowIndexer(new FakeFocusedSource(rows), [MakeSpec(0, "A")], RevealThreshold);

        // Act
        await indexer.BuildIndexAsync(CancellationToken.None);

        // Assert
        indexer.TotalMatchedRows.Should().Be(1250);
        indexer.GetSourceRow(1249).Should().Be(2498);
    }

    [Fact]
    public async Task BuildIndexAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var indexer = new FocusedTableFilterRowIndexer(new FakeFocusedSource([["[0]", "A"]]), [MakeSpec(0, "A")], RevealThreshold);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => indexer.BuildIndexAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        indexer.TotalMatchedRows.Should().Be(0);
    }

    [Fact]
    public async Task BuildIndexAsync_WithSingleBatch_FiresProgressChangedOnceWithFinalCounts()
    {
        // Arrange
        var source = new FakeFocusedSource([["[0]", "A"], ["[1]", "B"]]);
        var indexer = new FocusedTableFilterRowIndexer(source, [MakeSpec(0, "A")], RevealThreshold);
        List<(int scannedRows, int totalRows)> events = [];
        indexer.ProgressChanged += (scannedRows, totalRows) => events.Add((scannedRows, totalRows));

        // Act
        await indexer.BuildIndexAsync(CancellationToken.None);

        // Assert
        events.Should().ContainSingle().Which.Should().Be((2, 2));
    }

    [Fact]
    public async Task BuildIndexAsync_WithMultipleBatches_FirstAndLastProgressReportBatchBoundaries()
    {
        // Arrange
        var rows = Enumerable.Range(0, 2500).Select(i => new[] { $"[{i}]", "A" }).ToArray();
        var indexer = new FocusedTableFilterRowIndexer(new FakeFocusedSource(rows), [MakeSpec(0, "A")], RevealThreshold);
        List<(int scannedRows, int totalRows)> events = [];
        indexer.ProgressChanged += (scannedRows, totalRows) => events.Add((scannedRows, totalRows));

        // Act
        await indexer.BuildIndexAsync(CancellationToken.None);

        // Assert — the first batch always notifies; the final batch always notifies
        // with the full row count regardless of the throttle interval.
        events[0].Should().Be((1000, 2500));
        events[^1].Should().Be((2500, 2500));
    }

    [Fact]
    public async Task BuildIndexAsync_WithEmptySource_CompletesWithoutProgress()
    {
        // Arrange
        var indexer = new FocusedTableFilterRowIndexer(new FakeFocusedSource([]), [MakeSpec(0, "A")], RevealThreshold);
        List<(int scannedRows, int totalRows)> events = [];
        indexer.ProgressChanged += (scannedRows, totalRows) => events.Add((scannedRows, totalRows));

        // Act
        await indexer.BuildIndexAsync(CancellationToken.None);

        // Assert
        indexer.TotalMatchedRows.Should().Be(0);
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildIndexAsync_WithEmptySourceAndCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var indexer = new FocusedTableFilterRowIndexer(new FakeFocusedSource([]), [MakeSpec(0, "A")], RevealThreshold);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => indexer.BuildIndexAsync(cts.Token);

        // Assert — the final token check covers the empty source, where the loop never runs
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BuildIndexAsync_CancelledDuringFinalBatch_ThrowsWithoutFinalProgressNotification()
    {
        // Arrange — 2000 rows = 2 batches; the gate blocks on the final row of the final batch
        var rows = Enumerable.Range(0, 2000).Select(i => new[] { $"[{i}]", "A" }).ToArray();
        using var source = new GatedFocusedSource(rows, gateRowIndex: 1999);
        var indexer = new FocusedTableFilterRowIndexer(source, [MakeSpec(0, "A")], RevealThreshold);
        List<(int scannedRows, int totalRows)> events = [];
        indexer.ProgressChanged += (scannedRows, totalRows) => events.Add((scannedRows, totalRows));
        using var cts = new CancellationTokenSource();

        // Act
        var build = Task.Run(() => indexer.BuildIndexAsync(cts.Token));
        try
        {
            await source.GateReached.Task;
            await cts.CancelAsync();
            source.ReleaseGate();

            // Assert — the in-flight batch finishes, then the post-yield token check throws
            // before the final progress notification fires
            var assertBuild = () => build;
            await assertBuild.Should().ThrowAsync<OperationCanceledException>();
            events.Should().ContainSingle().Which.Should().Be((1000, 2000));
            indexer.TotalMatchedRows.Should().Be(2000);
        }
        finally
        {
            // every exit releases the gate, cancels, and joins the worker before the
            // gate event and shared test state are disposed
            source.ReleaseGate();
            await cts.CancelAsync();
            await JoinScanWorkerAsync(build);
        }
    }

    [Fact]
    public async Task BuildIndexAsync_CancelledMidScan_StopsReadingSourceAfterInFlightBatch()
    {
        // Arrange — 3000 rows = 3 batches; the gate blocks on the last row of batch 2
        var rows = Enumerable.Range(0, 3000).Select(i => new[] { $"[{i}]", "A" }).ToArray();
        using var source = new GatedFocusedSource(rows, gateRowIndex: 1999);
        var indexer = new FocusedTableFilterRowIndexer(source, [MakeSpec(0, "A")], RevealThreshold);
        using var cts = new CancellationTokenSource();

        // Act
        var build = Task.Run(() => indexer.BuildIndexAsync(cts.Token));
        try
        {
            await source.GateReached.Task;
            await cts.CancelAsync();
            source.ReleaseGate();

            // Assert — one read per row: batch 3 is never read after cancellation
            var assertBuild = () => build;
            await assertBuild.Should().ThrowAsync<OperationCanceledException>();
            source.ReadCount.Should().Be(2000);
            indexer.TotalMatchedRows.Should().Be(2000);
        }
        finally
        {
            source.ReleaseGate();
            await cts.CancelAsync();
            await JoinScanWorkerAsync(build);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void GetSourceRow_BeforeBuildOrOutOfRange_ReturnsMinusOne(int filteredRow)
    {
        // Arrange
        var indexer = new FocusedTableFilterRowIndexer(new FakeFocusedSource([["[0]", "A"]]), [MakeSpec(0, "A")], RevealThreshold);

        // Act
        var sourceRow = indexer.GetSourceRow(filteredRow);

        // Assert
        sourceRow.Should().Be(-1);
    }

    [Fact]
    public async Task BuildIndexAsync_WhenMatchesReachThreshold_CompletesThresholdReachedMidScan()
    {
        // Arrange — threshold 2 of 3 matches; the gate blocks on the final row, after the threshold
        var rows = Enumerable.Range(0, 3).Select(static i => new[] { $"[{i}]", "A" }).ToArray();
        using var source = new GatedFocusedSource(rows, gateRowIndex: 2);
        var indexer = new FocusedTableFilterRowIndexer(source, [MakeSpec(0, "A")], 2);

        // Act
        var build = Task.Run(() => indexer.BuildIndexAsync(CancellationToken.None));
        try
        {
            await indexer.ThresholdReached.WaitAsync(TimeSpan.FromSeconds(10));

            // Assert — the threshold completes while the scan is provably still running
            build.IsCompleted.Should().BeFalse();
            source.ReleaseGate();
            await build;
            indexer.ThresholdReached.Status.Should().Be(TaskStatus.RanToCompletion);
        }
        finally
        {
            source.ReleaseGate();
            await JoinScanWorkerAsync(build);
        }
    }

    [Fact]
    public async Task BuildIndexAsync_WithFewerMatchesThanThreshold_CancelsThresholdReachedOnCompletion()
    {
        // Arrange — a single match never reaches the threshold of 2
        var indexer = new FocusedTableFilterRowIndexer(
            new FakeFocusedSource([["[0]", "A"], ["[1]", "B"]]), [MakeSpec(0, "A")], 2);

        // Act
        await indexer.BuildIndexAsync(CancellationToken.None);

        // Assert — a successful sub-threshold scan cancels the wait instead of leaving it pending
        indexer.ThresholdReached.Status.Should().Be(TaskStatus.Canceled);
        indexer.TotalMatchedRows.Should().Be(1);
    }

    [Fact]
    public async Task BuildIndexAsync_WhenCancelledBelowThreshold_CancelsThresholdReached()
    {
        // Arrange — one match against threshold 2; the gate blocks before the batch can finish
        string[][] rows =
        [
            ["[0]", "A"],
            ["[1]", "B"],
        ];
        using var source = new GatedFocusedSource(rows, gateRowIndex: 1);
        var indexer = new FocusedTableFilterRowIndexer(source, [MakeSpec(0, "A")], 2);
        using var cts = new CancellationTokenSource();

        // Act
        var build = Task.Run(() => indexer.BuildIndexAsync(cts.Token));
        try
        {
            await source.GateReached.Task;
            await cts.CancelAsync();
            source.ReleaseGate();

            // Assert — the canceled scan cancels the pending threshold wait as well
            var assertBuild = () => build;
            await assertBuild.Should().ThrowAsync<OperationCanceledException>();
            indexer.ThresholdReached.Status.Should().Be(TaskStatus.Canceled);
        }
        finally
        {
            source.ReleaseGate();
            await cts.CancelAsync();
            await JoinScanWorkerAsync(build);
        }
    }

    [Fact]
    public async Task BuildIndexAsync_WhenCancelledAfterThresholdReached_KeepsThresholdCompleted()
    {
        // Arrange — threshold 1 fires at the first match; the gate blocks on the next row
        string[][] rows =
        [
            ["[0]", "A"],
            ["[1]", "A"],
        ];
        using var source = new GatedFocusedSource(rows, gateRowIndex: 1);
        var indexer = new FocusedTableFilterRowIndexer(source, [MakeSpec(0, "A")], 1);
        using var cts = new CancellationTokenSource();

        // Act
        var build = Task.Run(() => indexer.BuildIndexAsync(cts.Token));
        try
        {
            await indexer.ThresholdReached.WaitAsync(TimeSpan.FromSeconds(10));
            await source.GateReached.Task;
            await cts.CancelAsync();
            source.ReleaseGate();

            // Assert — the late cancel cannot undo an already completed threshold wait
            var assertBuild = () => build;
            await assertBuild.Should().ThrowAsync<OperationCanceledException>();
            indexer.ThresholdReached.Status.Should().Be(TaskStatus.RanToCompletion);
        }
        finally
        {
            source.ReleaseGate();
            await cts.CancelAsync();
            await JoinScanWorkerAsync(build);
        }
    }

    [Fact]
    public async Task BuildIndexAsync_WhenSourceFaultsBeforeThreshold_ThrowsAndCancelsThresholdReached()
    {
        // Arrange — one match stays below threshold 2; the second row's read faults
        var indexer = new FocusedTableFilterRowIndexer(
            new FaultingFocusedSource([["[0]", "A"], ["[1]", "A"]], faultRowIndex: 1), [MakeSpec(0, "A")], 2);

        // Act
        var act = () => indexer.BuildIndexAsync(CancellationToken.None);

        // Assert — the fault propagates and the finally block cancels the pending threshold wait
        await act.Should().ThrowAsync<InvalidOperationException>();
        indexer.ThresholdReached.Status.Should().Be(TaskStatus.Canceled);
    }

    [Fact]
    public async Task BuildIndexAsync_WhenSourceFaultsAfterThresholdReached_ThrowsAndKeepsThresholdCompleted()
    {
        // Arrange — threshold 1 fires at the first match; the second row's read faults
        var indexer = new FocusedTableFilterRowIndexer(
            new FaultingFocusedSource([["[0]", "A"], ["[1]", "A"]], faultRowIndex: 1), [MakeSpec(0, "A")], 1);

        // Act
        var act = () => indexer.BuildIndexAsync(CancellationToken.None);

        // Assert — the fault cannot undo an already completed threshold wait
        await act.Should().ThrowAsync<InvalidOperationException>();
        indexer.ThresholdReached.Status.Should().Be(TaskStatus.RanToCompletion);
    }
}

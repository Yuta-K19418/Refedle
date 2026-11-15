using System.Threading.Channels;
using AwesomeAssertions;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine.Filtering;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

// LazyFilterScanTracker in isolation: the overlay lifecycle on the CSV / JSON Lines table
// views, progress handling, and the stale-notification discard for every retirement path.
public sealed class LazyFilterScanTrackerTests
{
    /// <summary>
    /// Indexer double whose progress the test raises manually, so tick handling is observed
    /// without a running background build.
    /// </summary>
    private sealed class SteppingFilterRowIndexer : IFilterRowIndexer
    {
        public event Action<int, int>? ProgressChanged;

        public int TotalMatchedRows => 0;

        public int GetSourceRow(int filteredRow) => -1;

        public Task BuildIndexAsync(CancellationToken ct) => Task.CompletedTask;

        public void RaiseProgress(int scannedRows, int totalRows) =>
            ProgressChanged?.Invoke(scannedRows, totalRows);
    }

    /// <summary>
    /// <see cref="IFilterScanRunner"/> double whose scans stay pending until the test ends
    /// them, so completion handling is observed deterministically.
    /// </summary>
    private sealed class ControllableFilterScanRunner : IFilterScanRunner
    {
        private readonly List<TaskCompletionSource> _scans = [];
        private readonly List<CancellationToken> _tokens = [];

        public List<CancellationToken> Tokens => _tokens;

        public Task RunAsync(IFilterRowIndexer filterIndexer, CancellationToken ct)
        {
            _tokens.Add(ct);
            var scan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _scans.Add(scan);
            return scan.Task;
        }

        public void Complete(int callIndex) => _scans[callIndex].TrySetResult();

        public void Cancel(int callIndex) => _scans[callIndex].TrySetCanceled();

        public void End(int callIndex) =>
            _scans[callIndex].TrySetException(new InvalidOperationException("scan failed"));
    }

    /// <summary>
    /// UI-invoke capture read through a channel with a bounded wait, so a worker continuation
    /// that has not queued its callback yet fails the test instead of racing it.
    /// </summary>
    private sealed class AwaitableCallbackQueue
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private readonly Channel<Action> _channel = Channel.CreateUnbounded<Action>();

        public void Enqueue(Action callback) => _channel.Writer.TryWrite(callback);

        public async Task<Action> NextAsync()
        {
            using var cts = new CancellationTokenSource(Timeout);
            return await _channel.Reader.ReadAsync(cts.Token);
        }

        public async Task RunPendingAsync(int expectedCount)
        {
            for (var i = 0; i < expectedCount; i++)
            {
                (await NextAsync())();
            }

            _channel.Reader.TryPeek(out _).Should().BeFalse("no unexpected callback must remain queued");
        }
    }

    private sealed class TestTableView : MorphTableView;

    private sealed class TrackerHarness : IDisposable
    {
        private readonly Terminal.Gui.App.IApplication _app = TestAppFactory.Create();
        private readonly CancellationTokenSource _sessionCts = new();

        public TrackerHarness()
        {
            View = new TestTableView
            {
                Table = new PagedTableSource(new NumberedTableSource(5)),
            };
            CurrentView = View;
            Tracker = new LazyFilterScanTracker(
                Runner,
                Pending.Enqueue,
                () => _sessionCts.Token,
                () => RefreshCount++,
                Errors.Add,
                () => CurrentView);
        }

        public AwaitableCallbackQueue Pending { get; } = new();

        public ControllableFilterScanRunner Runner { get; } = new();

        public LazyFilterScanTracker Tracker { get; }

        public TestTableView View { get; }

        public View CurrentView { get; set; }

        public int RefreshCount { get; private set; }

        public List<string> Errors { get; } = [];

        public SteppingFilterRowIndexer Indexer { get; } = new();

        /// <summary>Simulates the next file session starting, which retires the old token.</summary>
        public void StartNextSession() => _sessionCts.Cancel();

        public void Dispose()
        {
            Tracker.Dispose();
            View.Dispose();
            _sessionCts.Dispose();
            _app.Dispose();
        }
    }

    private sealed class NumberedTableSource(long totalRows) : IExtendedTableSource
    {
        public long TotalRows => totalRows;
        public int Columns => 1;
        public string[] ColumnNames => ["a"];
        public string[] RawColumnNames => ["a"];
        public object this[long row, int col] => $"row{row}";
    }

    private static ProgressBar GetOverlayBar(TrackerHarness harness) =>
        harness.View.SubViews.OfType<ProgressBar>().Should().ContainSingle().Subject;

    [Fact]
    public void Constructor_WithNullFilterScanRunner_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup required)

        // Act
        var act = () => new LazyFilterScanTracker(
            null!,
            static _ => { },
            static () => CancellationToken.None,
            static () => { },
            static _ => { },
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUiThreadInvoke_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup required)

        // Act
        var act = () => new LazyFilterScanTracker(
            new ControllableFilterScanRunner(),
            null!,
            static () => CancellationToken.None,
            static () => { },
            static _ => { },
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullParentToken_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup required)

        // Act
        var act = () => new LazyFilterScanTracker(
            new ControllableFilterScanRunner(),
            static _ => { },
            null!,
            static () => { },
            static _ => { },
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRefreshStatusBarHints_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup required)

        // Act
        var act = () => new LazyFilterScanTracker(
            new ControllableFilterScanRunner(),
            static _ => { },
            static () => CancellationToken.None,
            null!,
            static _ => { },
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullShowError_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup required)

        // Act
        var act = () => new LazyFilterScanTracker(
            new ControllableFilterScanRunner(),
            static _ => { },
            static () => CancellationToken.None,
            static () => { },
            null!,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullGetCurrentView_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup required)

        // Act
        var act = () => new LazyFilterScanTracker(
            new ControllableFilterScanRunner(),
            static _ => { },
            static () => CancellationToken.None,
            static () => { },
            static _ => { },
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void StartScan_ShowsOverlayOnTableView()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var harness = new TrackerHarness();

        // Act
        harness.Tracker.StartScan(harness.Indexer, harness.View);

        // Assert — spinner, bar and percentage label sit on the table view while the scan runs
        GetOverlayBar(harness).Fraction.Should().Be(0f);
        harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
        harness.RefreshCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleProgress_WhenScanIsCurrent_UpdatesOverlay()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var harness = new TrackerHarness();
        harness.Tracker.StartScan(harness.Indexer, harness.View);
        var bar = GetOverlayBar(harness);

        // Act — the tick is enqueued synchronously on the calling thread
        harness.Indexer.RaiseProgress(4, 5);
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert
        bar.Fraction.Should().BeApproximately(0.8f, 0.001f);
        harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
    }

    [Fact]
    public async Task HandleProgress_AfterDetach_LeavesOverlayUntouched()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var harness = new TrackerHarness();
        harness.Tracker.StartScan(harness.Indexer, harness.View);
        var bar = GetOverlayBar(harness);

        // Act — the tick is queued, then the view is replaced before the dispatcher runs it
        harness.Indexer.RaiseProgress(4, 5);
        harness.Tracker.Detach();
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert — the stale tick must not update or re-create the retired overlay
        bar.Fraction.Should().Be(0f);
        harness.View.SubViews.OfType<ProgressBar>().Should().BeEmpty();
        harness.Tracker.IsRunningForCurrentView.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteScan_WhenSuccessful_RetiresOverlayAndRefreshesStatusBar()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var harness = new TrackerHarness();
        harness.Tracker.StartScan(harness.Indexer, harness.View);

        // Act
        harness.Runner.Complete(0);
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert
        harness.View.SubViews.OfType<ProgressBar>().Should().BeEmpty();
        harness.Tracker.IsRunningForCurrentView.Should().BeFalse();
        harness.RefreshCount.Should().Be(1);
    }

    [Fact]
    public async Task CompleteScan_WhenScanFaults_ShowsErrorViewAndRetiresOverlay()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var harness = new TrackerHarness();
        harness.Tracker.StartScan(harness.Indexer, harness.View);

        // Act
        harness.Runner.End(0);
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert — a faulted scan retires the overlay and surfaces its failure reason
        harness.View.SubViews.OfType<ProgressBar>().Should().BeEmpty();
        harness.Errors.Should().ContainSingle().Which.Should().Be("Filter scan failed: scan failed");
        harness.RefreshCount.Should().Be(0);
        harness.Tracker.IsRunningForCurrentView.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteScan_WhenScanIsCanceled_RetiresOverlayAndChangesNothingElse()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var harness = new TrackerHarness();
        harness.Tracker.StartScan(harness.Indexer, harness.View);

        // Act
        harness.Runner.Cancel(0);
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert — a canceled scan has no failure reason, so no error view and no refresh
        harness.View.SubViews.OfType<ProgressBar>().Should().BeEmpty();
        harness.Errors.Should().BeEmpty();
        harness.RefreshCount.Should().Be(0);
        harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteScan_WhenFaultBelongsToPreviousFileSession_IsDiscarded()
    {
        // Arrange — the view stays current, but the scan's file session has ended
        using var app = TestAppFactory.Create();
        using var harness = new TrackerHarness();
        harness.Tracker.StartScan(harness.Indexer, harness.View);
        harness.StartNextSession();

        // Act
        harness.Runner.End(0);
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert — the stale fault retires the overlay but must not swap in the error view
        harness.View.SubViews.OfType<ProgressBar>().Should().BeEmpty();
        harness.Errors.Should().BeEmpty();
        harness.RefreshCount.Should().Be(0);
        harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteScan_WhenViewWasReplaced_IgnoresStaleCompletion()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var harness = new TrackerHarness();
        harness.Tracker.StartScan(harness.Indexer, harness.View);
        harness.Tracker.Detach();

        // Act
        harness.Runner.Complete(0);
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert
        harness.RefreshCount.Should().Be(0);
        harness.Tracker.IsRunningForCurrentView.Should().BeFalse();
    }

    [Fact]
    public async Task Dispose_WhileScanRuns_RetiresOverlayAndIgnoresQueuedCompletion()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var harness = new TrackerHarness();
        harness.Tracker.StartScan(harness.Indexer, harness.View);

        // Act
        harness.Tracker.Dispose();
        harness.Runner.Complete(0);
        var runQueuedCompletion = () => harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert — the completion queued before disposal is discarded without throwing
        await runQueuedCompletion.Should().NotThrowAsync();
        harness.RefreshCount.Should().Be(0);
        harness.View.SubViews.OfType<ProgressBar>().Should().BeEmpty();
    }

    [Fact]
    public async Task StartScan_WhilePreviousScanStillAttached_ReplacesIt()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var harness = new TrackerHarness();
        harness.Tracker.StartScan(harness.Indexer, harness.View);

        // Act — a second scan on the same view retires the first one's presentation
        harness.Tracker.StartScan(new SteppingFilterRowIndexer(), harness.View);

        // Assert — one overlay remains, and the retired indexer's ticks no longer reach it
        harness.View.SubViews.OfType<ProgressBar>().Should().ContainSingle();
        harness.Indexer.RaiseProgress(4, 5);
        await harness.Pending.RunPendingAsync(expectedCount: 0);
        GetOverlayBar(harness).Fraction.Should().Be(0f);
        harness.Runner.Tokens[0].IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteScan_WhenPreviousScanSucceedsAfterReplacement_KeepsCurrentOverlay()
    {
        // Arrange
        using var scenario = new ReplacedScanScenario();

        // Act — the old scan ends after the replacement; its queued tick and completion then run
        scenario.Harness.Runner.Complete(0);
        await scenario.Harness.Pending.RunPendingAsync(expectedCount: 2);

        // Assert
        await scenario.AssertReplacementIntactAsync();
    }

    [Fact]
    public async Task CompleteScan_WhenPreviousScanFaultsAfterReplacement_KeepsCurrentOverlayAndShowsNoError()
    {
        // Arrange
        using var scenario = new ReplacedScanScenario();

        // Act
        scenario.Harness.Runner.End(0);
        await scenario.Harness.Pending.RunPendingAsync(expectedCount: 2);

        // Assert
        await scenario.AssertReplacementIntactAsync();
        scenario.Harness.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteScan_WhenPreviousScanIsCanceledAfterReplacement_KeepsCurrentOverlay()
    {
        // Arrange
        using var scenario = new ReplacedScanScenario();

        // Act
        scenario.Harness.Runner.Cancel(0);
        await scenario.Harness.Pending.RunPendingAsync(expectedCount: 2);

        // Assert
        await scenario.AssertReplacementIntactAsync();
    }

    /// <summary>
    /// Scan A has a progress tick queued, then scan B replaces it on the same view. The tests end
    /// A with each outcome and then check, through the shared assertion, that B is unaffected.
    /// </summary>
    private sealed class ReplacedScanScenario : IDisposable
    {
        private readonly Terminal.Gui.App.IApplication _app = TestAppFactory.Create();
        private readonly SteppingFilterRowIndexer _nextIndexer = new();

        public ReplacedScanScenario()
        {
            Harness.Tracker.StartScan(Harness.Indexer, Harness.View);
            Harness.Indexer.RaiseProgress(1, 5);
            Harness.Tracker.StartScan(_nextIndexer, Harness.View);
        }

        public TrackerHarness Harness { get; } = new();

        /// <summary>
        /// B keeps its overlay untouched by A's tick, still accepts progress and completes
        /// normally, and A's runner token was never canceled by the replacement.
        /// </summary>
        public async Task AssertReplacementIntactAsync()
        {
            GetOverlayBar(Harness).Fraction.Should().Be(0f);
            Harness.Runner.Tokens[0].IsCancellationRequested.Should().BeFalse();
            Harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
            _nextIndexer.RaiseProgress(4, 5);
            await Harness.Pending.RunPendingAsync(expectedCount: 1);
            GetOverlayBar(Harness).Fraction.Should().BeApproximately(0.8f, 0.001f);
            Harness.RefreshCount.Should().Be(0);

            Harness.Runner.Complete(1);
            await Harness.Pending.RunPendingAsync(expectedCount: 1);
            Harness.View.SubViews.OfType<ProgressBar>().Should().BeEmpty();
            Harness.Tracker.IsRunningForCurrentView.Should().BeFalse();
            Harness.RefreshCount.Should().Be(1);
        }

        public void Dispose()
        {
            Harness.Dispose();
            _app.Dispose();
        }
    }

    [Fact]
    public async Task HandleProgress_WhenRaisedOnWorkerThread_AppliesOnlyWhenQueuedCallbackRuns()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var harness = new TrackerHarness();
        harness.Tracker.StartScan(harness.Indexer, harness.View);
        var bar = GetOverlayBar(harness);

        // Act — the tick fires on a worker thread; nothing runs on the UI queue yet
        await Task.Run(() => harness.Indexer.RaiseProgress(4, 5));

        // Assert
        bar.Fraction.Should().Be(0f);
        await harness.Pending.RunPendingAsync(expectedCount: 1);
        bar.Fraction.Should().BeApproximately(0.8f, 0.001f);
    }

    [Fact]
    public async Task HandleProgress_WhenFileSessionEndedBeforeCallbackRuns_IsDiscarded()
    {
        // Arrange — the view stays current, but the file session ends after the tick is queued
        using var app = TestAppFactory.Create();
        using var harness = new TrackerHarness();
        harness.Tracker.StartScan(harness.Indexer, harness.View);
        var bar = GetOverlayBar(harness);
        harness.Indexer.RaiseProgress(4, 5);
        harness.StartNextSession();

        // Act
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert
        bar.Fraction.Should().Be(0f);
    }
}

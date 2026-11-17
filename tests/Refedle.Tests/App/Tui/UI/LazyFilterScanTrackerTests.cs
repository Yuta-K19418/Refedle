using AwesomeAssertions;
using Refedle.App.Tui.UI;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using static Refedle.TestHelpers.App.Tui.UI.LazyFilterScanTrackerHarnessAssertions;

namespace Refedle.Tests.App.Tui.UI;

// LazyFilterScanTracker in isolation: the deferred start behind the row-indexing wait, the
// overlay lifecycle on the CSV / JSON Lines table views, progress handling, and the
// stale-notification discard for every retirement path.
public sealed class LazyFilterScanTrackerTests
{
    /// <summary>
    /// Lets the pending row indexing finish and runs the queued scan start, so the test observes
    /// the tracker with the scan actually begun.
    /// </summary>
    private static async Task LetScanBeginAsync(LazyFilterScanTrackerHarness harness)
    {
        harness.CompleteIndexing();
        await harness.Pending.RunPendingAsync(expectedCount: 1);
    }

    [Fact]
    public void Constructor_WithNullFilterScanRunner_ThrowsArgumentNullException()
    {
        // Arrange
        using View host = new();

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            null!,
            static _ => { },
            static () => CancellationToken.None,
            static () => { },
            static _ => { },
            host,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUiThreadInvoke_ThrowsArgumentNullException()
    {
        // Arrange
        using View host = new();

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            new PendingFilterScanRunner(),
            null!,
            static () => CancellationToken.None,
            static () => { },
            static _ => { },
            host,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullParentToken_ThrowsArgumentNullException()
    {
        // Arrange
        using View host = new();

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            new PendingFilterScanRunner(),
            static _ => { },
            null!,
            static () => { },
            static _ => { },
            host,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRefreshStatusBarHints_ThrowsArgumentNullException()
    {
        // Arrange
        using View host = new();

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            new PendingFilterScanRunner(),
            static _ => { },
            static () => CancellationToken.None,
            null!,
            static _ => { },
            host,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullShowError_ThrowsArgumentNullException()
    {
        // Arrange
        using View host = new();

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            new PendingFilterScanRunner(),
            static _ => { },
            static () => CancellationToken.None,
            static () => { },
            null!,
            host,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullGetCurrentView_ThrowsArgumentNullException()
    {
        // Arrange
        using View host = new();

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            new PendingFilterScanRunner(),
            static _ => { },
            static () => CancellationToken.None,
            static () => { },
            static _ => { },
            host,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullOverlayHost_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup required)

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            new PendingFilterScanRunner(),
            static _ => { },
            static () => CancellationToken.None,
            static () => { },
            static _ => { },
            null!,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void StartScan_WhileIndexingRuns_ShowsNoOverlayAndStartsNoScan()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();

        // Act
        _ = harness.StartScanAsync();

        // Assert — while the row index is still building, the scan has not begun
        harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
        harness.Runner.Tokens.Should().BeEmpty();
        harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
        harness.RefreshCount.Should().Be(0);
    }

    [Fact]
    public async Task StartScan_WhenIndexingCompletes_ShowsOverlayOnOverlayHostAndStartsScan()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();

        // Act
        await LetScanBeginAsync(harness);

        // Assert — spinner and percentage label sit on the host container, never on
        // the table view whose scrollable content would carry them out of the viewport
        GetOverlayLabel(harness).Text.Should().Be(" Filtering… 0%");
        harness.OverlayHost.SubViews.OfType<ProgressBar>().Should().BeEmpty();
        harness.View.SubViews.OfType<Label>().Should().BeEmpty();
        harness.Runner.Tokens.Should().ContainSingle();
        harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
        harness.RefreshCount.Should().Be(0);
    }

    [Fact]
    public async Task StartScan_WhenIndexingAlreadyCompleted_StartsScan()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();

        // Act — the completed wait continues synchronously, queuing the scan start
        _ = harness.Tracker.StartScanAsync(harness.Indexer, harness.View, Task.CompletedTask);
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert
        GetOverlayLabel(harness).Text.Should().Be(" Filtering… 0%");
        harness.Runner.Tokens.Should().ContainSingle();
        harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
    }

    [Fact]
    public async Task StartScan_WhenViewWasReplacedWhileWaitingIndexing_StartsNoScan()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        harness.Tracker.Detach();

        // Act
        await LetScanBeginAsync(harness);

        // Assert — the stale scan start is discarded without overlay or build
        harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
        harness.Runner.Tokens.Should().BeEmpty();
        harness.Tracker.IsRunningForCurrentView.Should().BeFalse();
    }

    [Fact]
    public async Task StartScan_WhenFileSessionEndedWhileWaitingIndexing_StartsNoScan()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        Task deferredStart = harness.StartScanAsync();

        // Act — the file session ends while indexing is still running, then indexing finishes;
        // the awaited task is the synchronization point proving the wait abandoned the scan
        harness.StartNextSession();
        harness.CompleteIndexing();
        await deferredStart.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert — once the wait ended, nothing was ever queued: no scan starts, no overlay
        await harness.Pending.RunPendingAsync(expectedCount: 0);
        harness.Runner.Tokens.Should().BeEmpty();
        harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
        harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
    }

    [Fact]
    public async Task StartScan_WhenTrackerDisposedWhileWaitingIndexing_StartsNoScan()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        harness.Tracker.Dispose();

        // Act
        await LetScanBeginAsync(harness);

        // Assert — the scan start after disposal is discarded without overlay or build
        harness.Runner.Tokens.Should().BeEmpty();
        harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
    }

    [Fact]
    public async Task StartScan_WhenViewWasDetachedAfterStartIsQueued_DiscardsQueuedStart()
    {
        // Arrange — the scan start is queued and captured before the dispatcher runs it
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        harness.CompleteIndexing();
        Action queuedStart = await harness.Pending.NextAsync();
        harness.Tracker.Detach();

        // Act
        queuedStart();

        // Assert — the stale start neither runs a build nor shows an overlay
        harness.Runner.Tokens.Should().BeEmpty();
        harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
        harness.RefreshCount.Should().Be(0);
        harness.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task StartScan_WhenFileSessionEndsAfterStartIsQueued_DiscardsQueuedStart()
    {
        // Arrange — the scan start is queued and captured before the dispatcher runs it
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        harness.CompleteIndexing();
        Action queuedStart = await harness.Pending.NextAsync();
        harness.StartNextSession();

        // Act
        queuedStart();

        // Assert — the stale start neither runs a build nor shows an overlay
        harness.Runner.Tokens.Should().BeEmpty();
        harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
        harness.RefreshCount.Should().Be(0);
        harness.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task StartScan_WhenTrackerDisposedAfterStartIsQueued_DiscardsQueuedStart()
    {
        // Arrange — the scan start is queued and captured before the dispatcher runs it
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        harness.CompleteIndexing();
        Action queuedStart = await harness.Pending.NextAsync();
        harness.Tracker.Dispose();

        // Act
        Action act = queuedStart;

        // Assert — the start queued before disposal is discarded without throwing
        act.Should().NotThrow();
        harness.Runner.Tokens.Should().BeEmpty();
        harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
    }

    [Fact]
    public async Task StartScan_WhenReplacedByNextScanAfterStartIsQueued_StartsOnlyNextScan()
    {
        // Arrange — the scan start is queued and captured before the dispatcher runs it
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        harness.CompleteIndexing();
        Action queuedStart = await harness.Pending.NextAsync();

        // Act — a second scan replaces the waiting one, then the stale start runs
        _ = harness.Tracker.StartScanAsync(new SteppingFilterRowIndexer(), harness.View, Task.CompletedTask);
        queuedStart();
        await harness.Pending.RunPendingAsync(expectedCount: 1); // the replacement begins

        // Assert — only the replacement scan started, and it owns the overlay untouched
        harness.Runner.Tokens.Should().ContainSingle();
        GetOverlayLabel(harness).Text.Should().Be(" Filtering… 0%");
        harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
    }

    [Fact]
    public async Task HandleProgress_WhenScanIsCurrent_UpdatesOverlay()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        await LetScanBeginAsync(harness);
        Label label = GetOverlayLabel(harness);

        // Act — the tick is enqueued synchronously on the calling thread
        harness.Indexer.RaiseProgress(4, 5);
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert
        label.Text.Should().Be(" Filtering… 80%");
        harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
    }

    [Fact]
    public async Task HandleProgress_AfterDetach_LeavesOverlayUntouched()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        await LetScanBeginAsync(harness);
        Label label = GetOverlayLabel(harness);

        // Act — the tick is queued, then the view is replaced before the dispatcher runs it
        harness.Indexer.RaiseProgress(4, 5);
        harness.Tracker.Detach();
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert — the stale tick must not update or re-create the retired overlay
        label.Text.Should().Be(" Filtering… 0%");
        harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
        harness.Tracker.IsRunningForCurrentView.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteScan_WhenSuccessful_RetiresOverlayAndRefreshesStatusBar()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        await LetScanBeginAsync(harness);

        // Act
        harness.Runner.Complete(0);
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert
        harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
        harness.Tracker.IsRunningForCurrentView.Should().BeFalse();
        harness.RefreshCount.Should().Be(1);
    }

    [Fact]
    public async Task CompleteScan_WhenScanFaults_ShowsErrorViewAndRetiresOverlay()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        await LetScanBeginAsync(harness);

        // Act
        harness.Runner.End(0);
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert — a faulted scan retires the overlay and surfaces its failure reason
        harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
        harness.Errors.Should().ContainSingle().Which.Should().Be("Filter scan failed: scan failed");
        harness.RefreshCount.Should().Be(0);
        harness.Tracker.IsRunningForCurrentView.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteScan_WhenScanIsCanceled_RetiresOverlayAndChangesNothingElse()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        await LetScanBeginAsync(harness);

        // Act
        harness.Runner.Cancel(0);
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert — a canceled scan has no failure reason, so no error view and no refresh
        harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
        harness.Errors.Should().BeEmpty();
        harness.RefreshCount.Should().Be(0);
        harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteScan_WhenFaultBelongsToPreviousFileSession_IsDiscarded()
    {
        // Arrange — the view stays current, but the scan's file session has ended
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        await LetScanBeginAsync(harness);
        harness.StartNextSession();

        // Act
        harness.Runner.End(0);
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert — the stale fault retires the overlay but must not swap in the error view
        harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
        harness.Errors.Should().BeEmpty();
        harness.RefreshCount.Should().Be(0);
        harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteScan_WhenViewWasReplaced_IgnoresStaleCompletion()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        await LetScanBeginAsync(harness);
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
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        await LetScanBeginAsync(harness);

        // Act
        harness.Tracker.Dispose();
        harness.Runner.Complete(0);
        Func<Task> runQueuedCompletion = () => harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert — the completion queued before disposal is discarded without throwing
        await runQueuedCompletion.Should().NotThrowAsync();
        harness.RefreshCount.Should().Be(0);
        harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
    }

    [Fact]
    public async Task StartScan_WhilePreviousScanStillAttached_ReplacesIt()
    {
        // Arrange — the first scan is running when the second one replaces it
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        await LetScanBeginAsync(harness);

        // Act — a second scan on the same view retires the first one's presentation
        _ = harness.Tracker.StartScanAsync(new SteppingFilterRowIndexer(), harness.View, Task.CompletedTask);
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert — one overlay remains, and the retired indexer's ticks no longer reach it
        harness.OverlayHost.SubViews.OfType<Label>().Should().ContainSingle();
        harness.Indexer.RaiseProgress(4, 5);
        await harness.Pending.RunPendingAsync(expectedCount: 0);
        GetOverlayLabel(harness).Text.Should().Be(" Filtering… 0%");
        harness.Runner.Tokens[0].IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteScan_WhenPreviousScanSucceedsAfterReplacement_KeepsCurrentOverlay()
    {
        // Arrange
        using ReplacedScanScenario scenario = await ReplacedScanScenario.CreateAsync();

        // Act — the old scan ends after the replacement; its completion then runs
        scenario.Harness.Runner.Complete(0);
        await scenario.Harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert
        await scenario.AssertReplacementIntactAsync();
    }

    [Fact]
    public async Task CompleteScan_WhenPreviousScanFaultsAfterReplacement_KeepsCurrentOverlayAndShowsNoError()
    {
        // Arrange
        using ReplacedScanScenario scenario = await ReplacedScanScenario.CreateAsync();

        // Act
        scenario.Harness.Runner.End(0);
        await scenario.Harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert
        await scenario.AssertReplacementIntactAsync();
        scenario.Harness.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteScan_WhenPreviousScanIsCanceledAfterReplacement_KeepsCurrentOverlay()
    {
        // Arrange
        using ReplacedScanScenario scenario = await ReplacedScanScenario.CreateAsync();

        // Act
        scenario.Harness.Runner.Cancel(0);
        await scenario.Harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert
        await scenario.AssertReplacementIntactAsync();
    }

    /// <summary>
    /// Scan A is running with a progress tick queued, then scan B replaces it on the same view.
    /// The tests end A with each outcome and then check, through the shared assertion, that B is
    /// unaffected.
    /// </summary>
    private sealed class ReplacedScanScenario : IDisposable
    {
        private readonly Terminal.Gui.App.IApplication _app = TestAppFactory.Create();
        private readonly SteppingFilterRowIndexer _nextIndexer = new();

        public static async Task<ReplacedScanScenario> CreateAsync()
        {
            ReplacedScanScenario scenario = new();
            _ = scenario.Harness.StartScanAsync();
            scenario.Harness.CompleteIndexing();
            await scenario.Harness.Pending.RunPendingAsync(expectedCount: 1); // scan A begins

            scenario.Harness.Indexer.RaiseProgress(1, 5); // A's tick queued
            _ = scenario.Harness.Tracker.StartScanAsync(
                scenario._nextIndexer,
                scenario.Harness.View,
                Task.CompletedTask);
            await scenario.Harness.Pending.RunPendingAsync(expectedCount: 2); // stale tick, then B begins
            return scenario;
        }

        public LazyFilterScanTrackerHarness Harness { get; } = new();

        /// <summary>
        /// B keeps its overlay untouched by A's tick, still accepts progress and completes
        /// normally, and A's runner token was never canceled by the replacement.
        /// </summary>
        public async Task AssertReplacementIntactAsync()
        {
            GetOverlayLabel(Harness).Text.Should().Be(" Filtering… 0%");
            Harness.Runner.Tokens[0].IsCancellationRequested.Should().BeFalse();
            Harness.Tracker.IsRunningForCurrentView.Should().BeTrue();
            _nextIndexer.RaiseProgress(4, 5);
            await Harness.Pending.RunPendingAsync(expectedCount: 1);
            GetOverlayLabel(Harness).Text.Should().Be(" Filtering… 80%");
            Harness.RefreshCount.Should().Be(0);

            Harness.Runner.Complete(1);
            await Harness.Pending.RunPendingAsync(expectedCount: 1);
            Harness.OverlayHost.SubViews.OfType<Label>().Should().BeEmpty();
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
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        await LetScanBeginAsync(harness);
        Label label = GetOverlayLabel(harness);

        // Act — the tick fires on a worker thread; nothing runs on the UI queue yet
        await Task.Run(() => harness.Indexer.RaiseProgress(4, 5));

        // Assert
        label.Text.Should().Be(" Filtering… 0%");
        await harness.Pending.RunPendingAsync(expectedCount: 1);
        label.Text.Should().Be(" Filtering… 80%");
    }

    [Fact]
    public async Task HandleProgress_WhenFileSessionEndedBeforeCallbackRuns_IsDiscarded()
    {
        // Arrange — the view stays current, but the file session ends after the tick is queued
        using IApplication app = TestAppFactory.Create();
        using LazyFilterScanTrackerHarness harness = new();
        _ = harness.StartScanAsync();
        await LetScanBeginAsync(harness);
        Label label = GetOverlayLabel(harness);
        harness.Indexer.RaiseProgress(4, 5);
        harness.StartNextSession();

        // Act
        await harness.Pending.RunPendingAsync(expectedCount: 1);

        // Assert
        label.Text.Should().Be(" Filtering… 0%");
    }
}

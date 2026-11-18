using AwesomeAssertions;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using static Refedle.TestHelpers.App.Tui.UI.LazyFilterScanTrackerHarnessAssertions;

namespace Refedle.Tests.App.Tui.UI;

public sealed partial class LazyFilterScanTrackerTests
{
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
}

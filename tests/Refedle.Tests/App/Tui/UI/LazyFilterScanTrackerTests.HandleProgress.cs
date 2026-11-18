using AwesomeAssertions;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using static Refedle.TestHelpers.App.Tui.UI.LazyFilterScanTrackerHarnessAssertions;

namespace Refedle.Tests.App.Tui.UI;

public sealed partial class LazyFilterScanTrackerTests
{
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

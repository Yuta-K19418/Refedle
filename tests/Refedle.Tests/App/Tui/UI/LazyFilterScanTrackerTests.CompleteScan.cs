using AwesomeAssertions;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

public sealed partial class LazyFilterScanTrackerTests
{
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
}

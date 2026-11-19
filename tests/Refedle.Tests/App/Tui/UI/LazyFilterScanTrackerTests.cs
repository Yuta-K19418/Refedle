using AwesomeAssertions;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.Views;
using static Refedle.TestHelpers.App.Tui.UI.LazyFilterScanTrackerHarnessAssertions;

namespace Refedle.Tests.App.Tui.UI;

// LazyFilterScanTracker in isolation: the deferred start behind the row-indexing wait, the
// overlay lifecycle on the CSV / JSON Lines table views, progress handling, and the
// stale-notification discard for every retirement path.
public sealed partial class LazyFilterScanTrackerTests
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
}

using System.Collections.Concurrent;
using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.Models.Actions;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

public sealed class FilterScanManagerTests
{
    [Fact]
    public async Task Dispose_CalledTwiceWithActiveScan_CancelsScanDisposesSessionAndDoesNotThrow()
    {
        // Arrange — the manager is driven directly, so only its own guard can make the
        // second Dispose safe (ViewManager.Dispose short-circuits repeated calls)
        using IApplication app = TestAppFactory.Create();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = GatedDrillDownData.CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FocusedTableTransformer transformer = FocusedTableTransformer.Create(
            new FocusedTableSource(drillDown),
            drillDown.Schema,
            drillDown.ActionStack,
            200);
        FocusedTableFilterRowIndexer indexer = transformer.FilterRowIndexer.Should().BeOfType<FocusedTableFilterRowIndexer>().Subject;
        TaskRecordingFilterScanRunner runner = new();
        using CancellationTokenSource parentCts = new();
        ConcurrentQueue<Action> captured = new();
        await using DirectScanScope scope = new(rows, runner);
        FilterScanManager manager = new(
            runner,
            captured.Enqueue,
            () => parentCts.Token,
            static (_, _) => throw new InvalidOperationException("The table view must not be created in this test."),
            view => scope.LoadingView = view,
            static () => { },
            static _ => { },
            scope.Host,
            static () => null);
        scope.Manager = manager;
        manager.StartScan(transformer, indexer, new TableStyle { AlwaysShowHeaders = true });
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // Act
        manager.Dispose();
        Action disposeAgain = () => manager.Dispose();
        rows.ReleaseGate();

        // Assert — the worker observed the cancellation after batches 1–2 only, and the
        // session-owned transformer is disposed
        disposeAgain.Should().NotThrow();
        Func<Task> assertScan = () => runner.TaskAt(0);
        await assertScan.Should().ThrowAsync<OperationCanceledException>();
        rows.ReadCount.Should().Be(2000);
        Func<object> readDisposedTransformer = () => transformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
        scope.BodyCompleted = true;
    }

    [Fact]
    public async Task Progress_WhenCurrentSessionIsCanceled_DoesNotUpdateOverlay()
    {
        // Arrange — the session stays the current one; only its CTS is canceled (parent token)
        using IApplication app = TestAppFactory.Create();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = GatedDrillDownData.CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FocusedTableTransformer transformer = FocusedTableTransformer.Create(
            new FocusedTableSource(drillDown),
            drillDown.Schema,
            drillDown.ActionStack,
            200);
        FocusedTableFilterRowIndexer indexer = transformer.FilterRowIndexer.Should().BeOfType<FocusedTableFilterRowIndexer>().Subject;
        TaskRecordingFilterScanRunner runner = new();
        using CancellationTokenSource parentCts = new();
        ConcurrentQueue<Action> captured = new();
        await using DirectScanScope scope = new(rows, runner);
        FilterScanManager manager = new(
            runner,
            captured.Enqueue,
            () => parentCts.Token,
            static (_, _) => throw new InvalidOperationException("The table view must not be created in this test."),
            view => scope.LoadingView = view,
            static () => { },
            static _ => { },
            scope.Host,
            static () => null);
        scope.Manager = manager;
        manager.StartScan(transformer, indexer, new TableStyle { AlwaysShowHeaders = true });
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // the threshold reveal and the first progress tick are both queued once the worker is blocked
        await WaitForCallbacksAsync(captured, expectedCount: 2);
        Label label = scope.Host.SubViews.OfType<Label>().Where(ProgressOverlayViews.IsProgressOverlayLabel).Should().ContainSingle().Subject;
        string textBefore = label.Text;

        // Act
        parentCts.Cancel();
        ExecuteRemainingCallbacks(captured);

        // Assert
        label.Text.Should().Be(textBefore);
        scope.BodyCompleted = true;
    }

    private static void ExecuteRemainingCallbacks(ConcurrentQueue<Action> captured)
    {
        while (captured.TryDequeue(out Action? callback))
        {
            callback();
        }
    }

    private static async Task WaitForCallbacksAsync(ConcurrentQueue<Action> captured, int expectedCount)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (captured.Count < expectedCount && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        captured.Count.Should().BeGreaterThanOrEqualTo(expectedCount);
    }

    /// <summary>
    /// Cleanup scope for tests that drive a <see cref="FilterScanManager"/> directly: on every
    /// exit it releases the gate, disposes the manager, joins the started workers with a
    /// bounded wait and disposes the loading view, running every step even if one fails.
    /// Cleanup failures surface only after the test body completed (<see cref="BodyCompleted"/>);
    /// otherwise they are dropped so they cannot replace the failure already propagating.
    /// </summary>
    private sealed class DirectScanScope(GatedDrillDownRows rows, TaskRecordingFilterScanRunner runner) : IAsyncDisposable
    {
        public FilterScanManager? Manager { get; set; }

        public Terminal.Gui.ViewBase.View Host { get; } = new();

        public Terminal.Gui.ViewBase.View? LoadingView { get; set; }

        public bool BodyCompleted { get; set; }

        public async ValueTask DisposeAsync()
        {
            List<Exception> failures = [];
            Capture(failures, rows.ReleaseGate);
            Capture(failures, () => Manager?.Dispose());
            foreach (Task task in runner.Tasks())
            {
                // WhenAny observes a canceled or faulted worker without throwing
                await Task.WhenAny(task.WaitAsync(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
                if (!task.IsCompleted)
                {
                    failures.Add(new TimeoutException("A scan worker did not terminate within the bounded cleanup wait."));
                }
            }

            Capture(failures, () => LoadingView?.Dispose());
            Capture(failures, Host.Dispose);
            if (BodyCompleted && failures.Count > 0)
            {
                throw new AggregateException(failures);
            }
        }

#pragma warning disable CA1031 // Every cleanup step must run; failures are collected and reported by the caller
        private static void Capture(List<Exception> failures, Action step)
        {
            try
            {
                step();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
#pragma warning restore CA1031
    }
}

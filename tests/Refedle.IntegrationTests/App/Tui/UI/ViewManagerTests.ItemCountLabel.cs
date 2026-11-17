using System.Threading.Channels;
using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.IntegrationTests.App.Tui.Workers.Schema;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.IntegrationTests.App.Tui.UI;

public sealed partial class ViewManagerTests
{
    /// <summary>
    /// Grace period for the negative queue assertions: wide enough for a late queueing to
    /// arrive and fail the test, short enough to keep the suite fast.
    /// </summary>
    private static readonly TimeSpan NothingQueuedGrace = TimeSpan.FromMilliseconds(250);

    private static TableSchema CreateItemCountSchema(DataFormat format) => new()
    {
        SourceFormat = format,
        Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }],
    };

    private static string? GetItemCountText(Window window) =>
        window.SubViews
            .OfType<Label>()
            .Select(static label => label.Text)
            .SingleOrDefault(static text => text.EndsWith(" items", StringComparison.Ordinal));

    private static FilterAction CreateColumnFilter(string value) =>
        FilterAction.Create("col1", FilterOperator.Equals, ComparisonType.Text, value).Value;

    private static DataRowIndexer BuildCsvIndexer(string filePath)
    {
        DataRowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        return indexer;
    }

    [Theory]
    [InlineData("a", "1 items")]
    [InlineData("missing", "0 items")]
    public async Task SwitchToCsvTable_WithCompletedRealFilter_ShowsMatchingRowCount(string filterValue, string expectedText)
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        string filePath = CreateTempFile(".csv", "col1\na\nb\n");
        DataRowIndexer indexer = BuildCsvIndexer(filePath);
        using ItemCountHarness harness = new(filePath, DataFormat.Csv, indexer, new FilterIndexingRunner(), CreateColumnFilter(filterValue));

        // Act
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        string? textWhileScanning = GetItemCountText(harness.Window);
        await harness.Queue.RunUntilAsync(() => GetItemCountText(harness.Window) is not null);

        // Assert
        textWhileScanning.Should().BeNull();
        GetItemCountText(harness.Window).Should().Be(expectedText);
    }

    [Theory]
    [InlineData("a", "1 items")]
    [InlineData("missing", "0 items")]
    public async Task SwitchToJsonLinesTableView_WithCompletedRealFilter_ShowsMatchingRowCount(string filterValue, string expectedText)
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        string filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n{\"col1\": \"b\"}\n");
        MockRowIndexer indexer = new(filePath, 2);
        using ItemCountHarness harness = new(filePath, DataFormat.JsonLines, indexer, new FilterIndexingRunner(), CreateColumnFilter(filterValue));

        // Act
        harness.ViewManager.SwitchToJsonLinesTableView(indexer, CreateItemCountSchema(DataFormat.JsonLines));
        string? textWhileScanning = GetItemCountText(harness.Window);
        await harness.Queue.RunUntilAsync(() => GetItemCountText(harness.Window) is not null);

        // Assert
        textWhileScanning.Should().BeNull();
        GetItemCountText(harness.Window).Should().Be(expectedText);
    }

    [Fact]
    public async Task SwitchToCsvTable_WhileFilterScanRuns_HidesItemCountUntilScanCompletes()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        ControllableFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\nb\n"));
        using ItemCountHarness harness = new(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));

        // Act
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        string? textWhileScanning = GetItemCountText(harness.Window);
        await harness.Queue.RunNextAsync(); // indexing already finished, so the scan begins
        runner.Complete(0);
        await harness.Queue.RunNextAsync();

        // Assert
        textWhileScanning.Should().BeNull();
        GetItemCountText(harness.Window).Should().NotBeNull();
    }

    [Fact]
    public async Task SwitchToJsonLinesTableView_WhileFilterScanRuns_HidesItemCountUntilScanCompletes()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        ControllableFilterScanRunner runner = new();
        string filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        MockRowIndexer indexer = new(filePath, 1);
        using ItemCountHarness harness = new(filePath, DataFormat.JsonLines, indexer, runner, CreateColumnFilter("a"));

        // Act
        harness.ViewManager.SwitchToJsonLinesTableView(indexer, CreateItemCountSchema(DataFormat.JsonLines));
        string? textWhileScanning = GetItemCountText(harness.Window);
        await harness.Queue.RunNextAsync(); // indexing already finished, so the scan begins
        runner.Complete(0);
        await harness.Queue.RunNextAsync();

        // Assert
        textWhileScanning.Should().BeNull();
        GetItemCountText(harness.Window).Should().NotBeNull();
    }

    [Fact]
    public async Task SwitchToCsvTable_WithFaultedScan_ShowsErrorViewWithFailureReason()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        ControllableFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        using ItemCountHarness harness = new(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        await harness.Queue.RunNextAsync(); // indexing already finished, so the scan begins

        // Act
        runner.End(0, ScanOutcome.Faulted);
        await harness.Queue.RunNextAsync();

        // Assert
        harness.State.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        View? errorView = harness.ViewManager.GetCurrentView();
        errorView.Should().NotBeNull();
        errorView.Text.Should().Be("Filter scan failed: scan failed");
        GetItemCountText(harness.Window).Should().BeNull();
    }

    [Fact]
    public async Task SwitchToJsonLinesTableView_WithFaultedScan_ShowsErrorViewWithFailureReason()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        ControllableFilterScanRunner runner = new();
        string filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        MockRowIndexer indexer = new(filePath, 1);
        using ItemCountHarness harness = new(filePath, DataFormat.JsonLines, indexer, runner, CreateColumnFilter("a"));
        TableSchema schema = CreateItemCountSchema(DataFormat.JsonLines);
        harness.State.CompleteJsonLinesSchemaScan(schema);
        harness.ViewManager.SwitchToJsonLinesTableView(indexer, schema);
        harness.State.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
        await harness.Queue.RunNextAsync(); // indexing already finished, so the scan begins

        // Act
        runner.End(0, ScanOutcome.Faulted);
        await harness.Queue.RunNextAsync();

        // Assert
        harness.State.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        View? errorView = harness.ViewManager.GetCurrentView();
        errorView.Should().NotBeNull();
        errorView.Text.Should().Be("Filter scan failed: scan failed");
        GetItemCountText(harness.Window).Should().BeNull();
    }

    [Fact]
    public async Task SwitchToCsvTable_WithCanceledScan_KeepsTableAndItemCountHidden()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        ControllableFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        using ItemCountHarness harness = new(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        await harness.Queue.RunNextAsync(); // indexing already finished, so the scan begins
        View? tableView = harness.ViewManager.GetCurrentView();

        // Act
        runner.End(0, ScanOutcome.Canceled);
        await harness.Queue.RunNextAsync();

        // Assert
        harness.State.CurrentMode.Should().Be(ViewMode.CsvTable);
        harness.ViewManager.GetCurrentView().Should().BeSameAs(tableView);
        GetItemCountText(harness.Window).Should().BeNull();
    }

    [Fact]
    public async Task SwitchToCsvTable_WhenFaultedScanIsSuperseded_KeepsNewView()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        ControllableFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        TableSchema schema = CreateItemCountSchema(DataFormat.Csv);
        using ItemCountHarness harness = new(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, schema);
        await harness.Queue.RunNextAsync(); // indexing already finished, so the first scan begins
        harness.ViewManager.SwitchToCsvTable(indexer, schema);
        await harness.Queue.RunNextAsync(); // the replacement scan begins
        View? newView = harness.ViewManager.GetCurrentView();

        // Act
        runner.End(0, ScanOutcome.Faulted);
        await harness.Queue.RunNextAsync();

        // Assert
        harness.State.CurrentMode.Should().Be(ViewMode.CsvTable);
        harness.ViewManager.GetCurrentView().Should().BeSameAs(newView);
        GetItemCountText(harness.Window).Should().BeNull("the replacement scan is still running");
    }

    [Fact]
    public async Task SwitchToCsvTable_WhenFaultedScanBelongsToPreviousFileSession_KeepsCurrentSession()
    {
        // Arrange - the view is still the old one while the next file loads
        using IApplication app = TestAppFactory.Create();
        ControllableFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        using ItemCountHarness harness = new(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        await harness.Queue.RunNextAsync(); // indexing already finished, so the scan begins
        View? oldView = harness.ViewManager.GetCurrentView();
        runner.End(0, ScanOutcome.Faulted);
        Action callback = await harness.Queue.NextAsync();
        string nextFilePath = CreateTempFile(".csv", "col1\nb\n");
        harness.State.StartNewFile(nextFilePath);

        // Act
        callback();

        // Assert
        harness.State.CurrentMode.Should().Be(ViewMode.CsvTable);
        harness.State.CurrentFilePath.Should().Be(nextFilePath);
        harness.ViewManager.GetCurrentView().Should().BeSameAs(oldView);
        GetItemCountText(harness.Window).Should().BeNull();
    }

    [Fact]
    public async Task SwitchToCsvTable_WhenViewManagerIsDisposedBeforeFault_DoesNotShowError()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        ControllableFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        using ItemCountHarness harness = new(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        await harness.Queue.RunNextAsync(); // indexing already finished, so the scan begins
        harness.ViewManager.Dispose();
        runner.End(0, ScanOutcome.Faulted);
        Action callback = await harness.Queue.NextAsync();

        // Act
        Action act = () => callback();

        // Assert
        act.Should().NotThrow();
        harness.State.CurrentMode.Should().Be(ViewMode.CsvTable);
    }

    [Fact]
    public async Task SwitchToCsvTable_WhenScanIsSuperseded_IgnoresStaleCompletion()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        ControllableFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        TableSchema schema = CreateItemCountSchema(DataFormat.Csv);
        using ItemCountHarness harness = new(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, schema);
        await harness.Queue.RunNextAsync(); // indexing already finished, so the first scan begins
        harness.ViewManager.SwitchToCsvTable(indexer, schema);
        await harness.Queue.RunNextAsync(); // the replacement scan begins

        // Act
        runner.Complete(0);
        await harness.Queue.RunNextAsync();
        string? textAfterStaleCompletion = GetItemCountText(harness.Window);
        runner.Complete(1);
        await harness.Queue.RunNextAsync();

        // Assert
        textAfterStaleCompletion.Should().BeNull();
        GetItemCountText(harness.Window).Should().NotBeNull();
    }

    [Fact]
    public async Task SwitchToCsvTable_WhenViewManagerIsDisposedBeforeCompletion_IgnoresCompletion()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        ControllableFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        using ItemCountHarness harness = new(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        await harness.Queue.RunNextAsync(); // indexing already finished, so the scan begins

        // Act
        harness.ViewManager.Dispose();
        runner.Complete(0);
        await harness.Queue.RunNextAsync();

        // Assert
        GetItemCountText(harness.Window).Should().BeNull();
    }

    [Fact]
    public void SwitchToFocusedTable_WithoutFilter_ShowsFocusedTableRowCount()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        string filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 12);
        using ItemCountHarness harness = new(filePath, DataFormat.JsonLines, new MockRowIndexer(filePath, 5000), new FilterIndexingRunner(), filter: null);
        harness.State.EnterFocusedTable(drillDown);

        // Act
        harness.ViewManager.SwitchToFocusedTable(drillDown);

        // Assert
        GetItemCountText(harness.Window).Should().Be("12 items");
    }

    [Theory]
    [InlineData("Alice3", "1 items")]
    [InlineData("Nobody", "0 items")]
    public async Task SwitchToFocusedTable_WithFilterBelowThreshold_HidesItemCountUntilCompletion(
        string filterValue,
        string expectedText)
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        string filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        FilterAction filter = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, filterValue).Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using ItemCountHarness harness = new(filePath, DataFormat.JsonLines, new MockRowIndexer(filePath, 5000), new FilterIndexingRunner(), filter: null);
        harness.State.EnterFocusedTable(drillDown);

        // Act
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        string? textWhileScanning = GetItemCountText(harness.Window);
        await harness.Queue.RunUntilAsync(() => IsCompletedFocusedTable(harness.ViewManager));

        // Assert
        textWhileScanning.Should().BeNull();
        GetItemCountText(harness.Window).Should().Be(expectedText);
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFilterRevealedMidScan_ShowsItemCountOnlyAfterCompletion()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        string filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = CreateMixedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 50)
            with
        { ActionStack = [filter] };
        using ItemCountHarness harness = new(filePath, DataFormat.JsonLines, new MockRowIndexer(filePath, 5000), new FilterIndexingRunner(), filter: null);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);

        // Act — the scan queues a reveal, two progress ticks and the completion, in that order
        Action reveal = await harness.Queue.NextAsync();
        Action firstTick = await harness.Queue.NextAsync();
        Action finalTick = await harness.Queue.NextAsync();
        Action completion = await harness.Queue.NextAsync();
        reveal();
        firstTick();
        finalTick();
        string? textWhileTableIsRevealed = GetItemCountText(harness.Window);
        completion();

        // Assert
        harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>();
        textWhileTableIsRevealed.Should().BeNull();
        GetItemCountText(harness.Window).Should().Be("250 items");
    }

    private static bool IsCompletedFocusedTable(ViewManager viewManager) =>
        viewManager.GetCurrentView() is FocusedTableView { SuperView: { } host } && !SelfAndDescendants(host).Any(IsProgressOverlayLabel);

    /// <summary>
    /// Signal-based replacement for polling: each captured UI-thread callback is read from a channel
    /// with a bounded wait, so a callback that never arrives fails the test instead of hanging it.
    /// </summary>
    private sealed class CallbackQueue
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private readonly Channel<Action> _channel = Channel.CreateUnbounded<Action>();

        public void Enqueue(Action callback) => _channel.Writer.TryWrite(callback);

        public async Task<Action> NextAsync()
        {
            using CancellationTokenSource cts = new(Timeout);
            return await _channel.Reader.ReadAsync(cts.Token);
        }

        public async Task RunNextAsync() => (await NextAsync())();

        public async Task RunUntilAsync(Func<bool> isDone)
        {
            while (!isDone())
            {
                await RunNextAsync();
            }
        }

        /// <summary>
        /// Fails when any callback arrives within <paramref name="grace"/>; a callback that
        /// arrives later than the grace period is not detected by this check.
        /// </summary>
        public async Task AssertNothingQueuedAsync(TimeSpan grace)
        {
            using CancellationTokenSource cts = new(grace);
            Func<Task<Action>> read = async () => await _channel.Reader.ReadAsync(cts.Token);
            await read.Should().ThrowAsync<OperationCanceledException>(
                "no callback must be queued within the grace period");
        }
    }

    /// <summary>
    /// <see cref="IFilterScanRunner"/> double whose scans stay pending until the test completes,
    /// faults or cancels the call with the given index.
    /// </summary>
    private sealed class ControllableFilterScanRunner : IFilterScanRunner
    {
        private readonly List<TaskCompletionSource> _scans = [];

        public Task RunAsync(IFilterRowIndexer filterIndexer, CancellationToken ct)
        {
            TaskCompletionSource scan = new();
            _scans.Add(scan);
            return scan.Task;
        }

        public void Complete(int callIndex) => _scans[callIndex].SetResult();

        public void End(int callIndex, ScanOutcome outcome)
        {
            _ = outcome == ScanOutcome.Faulted
                ? _scans[callIndex].TrySetException(new InvalidOperationException("scan failed"))
                : _scans[callIndex].TrySetCanceled();
        }
    }

    public enum ScanOutcome
    {
        Faulted,
        Canceled,
    }

    /// <summary>
    /// ViewManager whose UI-thread invoke is captured in a <see cref="CallbackQueue"/>, so scan
    /// notifications run only when the test executes them. The optional filter is put on the Action Stack.
    /// </summary>
    private sealed class ItemCountHarness : IDisposable
    {
        private readonly StatusBar _statusBar = new();

        public ItemCountHarness(
            string filePath,
            DataFormat format,
            IRowIndexer rawIndexer,
            IFilterScanRunner runner,
            FilterAction? filter)
        {
            State.StartNewFile(filePath);
            State.CompleteCsvLoad(rawIndexer, CreateItemCountSchema(format));
            if (filter is not null)
            {
                State.AddMorphAction(filter);
            }

            Window.Add(_statusBar);
            ModeController modeController = new(State, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager = new ViewManager(Window, State, modeController, runner, Queue.Enqueue);
        }

        public AppState State { get; } = new();
        public Window Window { get; } = new();
        public CallbackQueue Queue { get; } = new();
        public ViewManager ViewManager { get; }

        public void Dispose()
        {
            ViewManager.Dispose();
            Window.Dispose();
            _statusBar.Dispose();
            State.Dispose();
        }
    }
}

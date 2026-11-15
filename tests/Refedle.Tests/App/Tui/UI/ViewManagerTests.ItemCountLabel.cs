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
using Refedle.Tests.App.Tui.Workers.Schema;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

public sealed partial class ViewManagerTests
{
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
        var indexer = new DataRowIndexer(filePath);
        indexer.BuildIndex();
        return indexer;
    }

    [Theory]
    [InlineData("a", "1 items")]
    [InlineData("missing", "0 items")]
    public async Task SwitchToCsvTable_WithCompletedRealFilter_ShowsMatchingRowCount(string filterValue, string expectedText)
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var filePath = CreateTempFile(".csv", "col1\na\nb\n");
        var indexer = BuildCsvIndexer(filePath);
        using var harness = new ItemCountHarness(filePath, DataFormat.Csv, indexer, new FilterIndexingRunner(), CreateColumnFilter(filterValue));

        // Act
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        var textWhileScanning = GetItemCountText(harness.Window);
        await harness.Queue.RunNextAsync();

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
        using var app = TestAppFactory.Create();
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n{\"col1\": \"b\"}\n");
        var indexer = new MockRowIndexer(filePath, 2);
        using var harness = new ItemCountHarness(filePath, DataFormat.JsonLines, indexer, new FilterIndexingRunner(), CreateColumnFilter(filterValue));

        // Act
        harness.ViewManager.SwitchToJsonLinesTableView(indexer, CreateItemCountSchema(DataFormat.JsonLines));
        var textWhileScanning = GetItemCountText(harness.Window);
        await harness.Queue.RunNextAsync();

        // Assert
        textWhileScanning.Should().BeNull();
        GetItemCountText(harness.Window).Should().Be(expectedText);
    }

    [Fact]
    public async Task SwitchToCsvTable_WhileFilterScanRuns_HidesItemCountUntilScanCompletes()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\nb\n"));
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));

        // Act
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        var textWhileScanning = GetItemCountText(harness.Window);
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
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        var indexer = new MockRowIndexer(filePath, 1);
        using var harness = new ItemCountHarness(filePath, DataFormat.JsonLines, indexer, runner, CreateColumnFilter("a"));

        // Act
        harness.ViewManager.SwitchToJsonLinesTableView(indexer, CreateItemCountSchema(DataFormat.JsonLines));
        var textWhileScanning = GetItemCountText(harness.Window);
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
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));

        // Act
        runner.End(0, ScanOutcome.Faulted);
        await harness.Queue.RunNextAsync();

        // Assert
        harness.State.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        var errorView = harness.ViewManager.GetCurrentView();
        errorView.Should().NotBeNull();
        errorView.Text.Should().Be("Filter scan failed: scan failed");
        GetItemCountText(harness.Window).Should().BeNull();
    }

    [Fact]
    public async Task SwitchToJsonLinesTableView_WithFaultedScan_ShowsErrorViewWithFailureReason()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        var indexer = new MockRowIndexer(filePath, 1);
        using var harness = new ItemCountHarness(filePath, DataFormat.JsonLines, indexer, runner, CreateColumnFilter("a"));
        var schema = CreateItemCountSchema(DataFormat.JsonLines);
        harness.State.CompleteJsonLinesSchemaScan(schema);
        harness.ViewManager.SwitchToJsonLinesTableView(indexer, schema);
        harness.State.CurrentMode.Should().Be(ViewMode.JsonLinesTable);

        // Act
        runner.End(0, ScanOutcome.Faulted);
        await harness.Queue.RunNextAsync();

        // Assert
        harness.State.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        var errorView = harness.ViewManager.GetCurrentView();
        errorView.Should().NotBeNull();
        errorView.Text.Should().Be("Filter scan failed: scan failed");
        GetItemCountText(harness.Window).Should().BeNull();
    }

    [Fact]
    public async Task SwitchToCsvTable_WithCanceledScan_KeepsTableAndItemCountHidden()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        var tableView = harness.ViewManager.GetCurrentView();

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
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        var schema = CreateItemCountSchema(DataFormat.Csv);
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, schema);
        harness.ViewManager.SwitchToCsvTable(indexer, schema);
        var newView = harness.ViewManager.GetCurrentView();

        // Act
        runner.End(0, ScanOutcome.Faulted);
        await harness.Queue.RunNextAsync();

        // Assert
        harness.State.CurrentMode.Should().Be(ViewMode.CsvTable);
        harness.ViewManager.GetCurrentView().Should().BeSameAs(newView);
    }

    [Fact]
    public async Task SwitchToCsvTable_WhenFaultedScanBelongsToPreviousFileSession_KeepsCurrentSession()
    {
        // Arrange - the view is still the old one while the next file loads
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        var oldView = harness.ViewManager.GetCurrentView();
        runner.End(0, ScanOutcome.Faulted);
        var callback = await harness.Queue.NextAsync();
        var nextFilePath = CreateTempFile(".csv", "col1\nb\n");
        harness.State.StartNewFile(nextFilePath);

        // Act
        callback();

        // Assert
        harness.State.CurrentMode.Should().Be(ViewMode.CsvTable);
        harness.State.CurrentFilePath.Should().Be(nextFilePath);
        harness.ViewManager.GetCurrentView().Should().BeSameAs(oldView);
    }

    [Fact]
    public async Task SwitchToCsvTable_WhenViewManagerIsDisposedBeforeFault_DoesNotShowError()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        harness.ViewManager.Dispose();
        runner.End(0, ScanOutcome.Faulted);
        var callback = await harness.Queue.NextAsync();

        // Act
        var act = () => callback();

        // Assert
        act.Should().NotThrow();
        harness.State.CurrentMode.Should().Be(ViewMode.CsvTable);
    }

    [Fact]
    public async Task SwitchToCsvTable_WhenScanIsSuperseded_IgnoresStaleCompletion()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        var schema = CreateItemCountSchema(DataFormat.Csv);
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, schema);
        harness.ViewManager.SwitchToCsvTable(indexer, schema);

        // Act
        runner.Complete(0);
        await harness.Queue.RunNextAsync();
        var textAfterStaleCompletion = GetItemCountText(harness.Window);
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
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));

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
        using var app = TestAppFactory.Create();
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        var drillDown = CreateNumberedDrillDown(rowCount: 12);
        using var harness = new ItemCountHarness(filePath, DataFormat.JsonLines, new MockRowIndexer(filePath, 5000), new FilterIndexingRunner(), filter: null);
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
        using var app = TestAppFactory.Create();
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        var filter = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, filterValue).Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using var harness = new ItemCountHarness(filePath, DataFormat.JsonLines, new MockRowIndexer(filePath, 5000), new FilterIndexingRunner(), filter: null);
        harness.State.EnterFocusedTable(drillDown);

        // Act
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        var textWhileScanning = GetItemCountText(harness.Window);
        await harness.Queue.RunUntilAsync(() => IsCompletedFocusedTable(harness.ViewManager));

        // Assert
        textWhileScanning.Should().BeNull();
        GetItemCountText(harness.Window).Should().Be(expectedText);
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFilterRevealedMidScan_ShowsItemCountOnlyAfterCompletion()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = CreateMixedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 50)
            with
        { ActionStack = [filter] };
        using var harness = new ItemCountHarness(filePath, DataFormat.JsonLines, new MockRowIndexer(filePath, 5000), new FilterIndexingRunner(), filter: null);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);

        // Act — the scan queues a reveal, two progress ticks and the completion, in that order
        var reveal = await harness.Queue.NextAsync();
        var firstTick = await harness.Queue.NextAsync();
        var finalTick = await harness.Queue.NextAsync();
        var completion = await harness.Queue.NextAsync();
        reveal();
        firstTick();
        finalTick();
        var textWhileTableIsRevealed = GetItemCountText(harness.Window);
        completion();

        // Assert
        harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>();
        textWhileTableIsRevealed.Should().BeNull();
        GetItemCountText(harness.Window).Should().Be("250 items");
    }

    private static bool IsCompletedFocusedTable(ViewManager viewManager) =>
        viewManager.GetCurrentView() is FocusedTableView view && !view.SubViews.OfType<ProgressBar>().Any();

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
            using var cts = new CancellationTokenSource(Timeout);
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
            var scan = new TaskCompletionSource();
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
            var modeController = new ModeController(State, action => action(), TestSchemaScannerFactories.JsonLines);
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

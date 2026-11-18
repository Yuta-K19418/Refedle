using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.IntegrationTests.App.Tui.UI.ViewManagerSupport;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using static Refedle.IntegrationTests.App.Tui.UI.ViewManagerSupport.ViewManagerTestData;
using static Refedle.IntegrationTests.App.Tui.UI.ViewManagerSupport.ViewManagerTestViews;

namespace Refedle.IntegrationTests.App.Tui.UI;

public sealed partial class ViewManagerTests
{
    /// <summary>
    /// Grace period for the negative queue assertions: wide enough for a late queueing to
    /// arrive and fail the test, short enough to keep the suite fast.
    /// </summary>
    private static readonly TimeSpan NothingQueuedGrace = TimeSpan.FromMilliseconds(250);

    [Theory]
    [InlineData("a", "1 items")]
    [InlineData("missing", "0 items")]
    public async Task SwitchToCsvTable_WithCompletedRealFilter_ShowsMatchingRowCount(string filterValue, string expectedText)
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        string filePath = _tempFiles.Create(".csv", "col1\na\nb\n");
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
        string filePath = _tempFiles.Create(".jsonl", "{\"col1\": \"a\"}\n{\"col1\": \"b\"}\n");
        CompletedStubRowIndexer indexer = new(filePath, 2);
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
        OutcomeFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(_tempFiles.Create(".csv", "col1\na\nb\n"));
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
        OutcomeFilterScanRunner runner = new();
        string filePath = _tempFiles.Create(".jsonl", "{\"col1\": \"a\"}\n");
        CompletedStubRowIndexer indexer = new(filePath, 1);
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
        OutcomeFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(_tempFiles.Create(".csv", "col1\na\n"));
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
        OutcomeFilterScanRunner runner = new();
        string filePath = _tempFiles.Create(".jsonl", "{\"col1\": \"a\"}\n");
        CompletedStubRowIndexer indexer = new(filePath, 1);
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
        OutcomeFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(_tempFiles.Create(".csv", "col1\na\n"));
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
        OutcomeFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(_tempFiles.Create(".csv", "col1\na\n"));
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
        OutcomeFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(_tempFiles.Create(".csv", "col1\na\n"));
        using ItemCountHarness harness = new(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        await harness.Queue.RunNextAsync(); // indexing already finished, so the scan begins
        View? oldView = harness.ViewManager.GetCurrentView();
        runner.End(0, ScanOutcome.Faulted);
        Action callback = await harness.Queue.NextAsync();
        string nextFilePath = _tempFiles.Create(".csv", "col1\nb\n");
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
        OutcomeFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(_tempFiles.Create(".csv", "col1\na\n"));
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
        OutcomeFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(_tempFiles.Create(".csv", "col1\na\n"));
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
        OutcomeFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(_tempFiles.Create(".csv", "col1\na\n"));
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
        string filePath = _tempFiles.Create(".jsonl", "{\"col1\": \"a\"}\n");
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 12);
        using ItemCountHarness harness = new(filePath, DataFormat.JsonLines, new CompletedStubRowIndexer(filePath, 5000), new FilterIndexingRunner(), filter: null);
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
        string filePath = _tempFiles.Create(".jsonl", "{\"col1\": \"a\"}\n");
        FilterAction filter = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, filterValue).Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using ItemCountHarness harness = new(filePath, DataFormat.JsonLines, new CompletedStubRowIndexer(filePath, 5000), new FilterIndexingRunner(), filter: null);
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
        string filePath = _tempFiles.Create(".jsonl", "{\"col1\": \"a\"}\n");
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = CreateMixedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 50)
            with
        { ActionStack = [filter] };
        using ItemCountHarness harness = new(filePath, DataFormat.JsonLines, new CompletedStubRowIndexer(filePath, 5000), new FilterIndexingRunner(), filter: null);
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
}

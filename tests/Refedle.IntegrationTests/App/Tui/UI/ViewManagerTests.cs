using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonArray;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui;
using Refedle.TestHelpers.App.Tui.UI;
using Refedle.TestHelpers.App.Tui.Workers.Schema;
using Refedle.TestHelpers.IO;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using static Refedle.TestHelpers.App.Tui.UI.GatedDrillDownData;
using static Refedle.TestHelpers.App.Tui.UI.ProgressOverlayViews;

namespace Refedle.IntegrationTests.App.Tui.UI;

public sealed partial class ViewManagerTests : IDisposable
{
    /// <summary>
    /// Grace period for the negative queue assertions: wide enough for a late queueing to
    /// arrive and fail the test, short enough to keep the suite fast.
    /// </summary>
    private static readonly TimeSpan NothingQueuedGrace = TimeSpan.FromMilliseconds(250);

    private readonly TempFileCollection _tempFiles = new();

    public void Dispose() => _tempFiles.Dispose();

    [Fact]
    public void RefreshStatusBarHints_WithMorphTableView_IncludesMenuHint()
    {
        // Arrange
        string filePath = _tempFiles.Create(".jsonl", "{\"col1\": \"value\"}\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        using Window window = new();
        using StatusBar statusBar = new();
        window.Add(statusBar);
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };
        viewManager.SwitchToCsvTable(new CompletedStubRowIndexer(filePath), schema);

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        StatusBar? currentStatusBar = viewManager.GetCurrentStatusBar();
        currentStatusBar.Should().NotBeNull();
        IEnumerable<string> hints = Enumerable.Select(
                Enumerable.OfType<Shortcut>(currentStatusBar.SubViews),
                s => s.HelpText);
        hints.Should().Contain("x:Menu");
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenToggleFails_ShowsError()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        CompletedStubRowIndexer indexer = new("test.jsonl");
        using AppState state = new();
        state.BeginIndexedTreeLoad(indexer);
        state.EnterJsonLinesTree();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        await viewManager.ToggleJsonLinesModeAsync();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        viewManager.GetCurrentView()?.Text.Should().Be("No file is currently open");
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenModeBecomesTree_SwitchesToTreeView()
    {
        // Arrange
        string filePath = _tempFiles.Create(".jsonl", "{\"col1\": \"value\"}\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Setup a valid table state
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };
        CompletedStubRowIndexer indexer = new(filePath);
        state.BeginIndexedTreeLoad(indexer);
        state.CompleteJsonLinesSchemaScan(schema);

        // Act
        await viewManager.ToggleJsonLinesModeAsync();

        // Assert
        // After toggle, mode should become JsonLinesTree
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenModeBecomesTable_SwitchesToTableView()
    {
        // Arrange
        string filePath = _tempFiles.Create(".jsonl", "{\"col1\": \"value\"}\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        state.EnterJsonLinesTree();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Setup a valid tree state
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };
        CompletedStubRowIndexer indexer = new(filePath);
        state.BeginIndexedTreeLoad(indexer);
        state.ApplyRefinedSchema(schema);

        // Act
        await viewManager.ToggleJsonLinesModeAsync();

        // Assert
        // After toggle, mode should become JsonLinesTable
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
    }

    [Fact]
    public async Task ToggleJsonArrayModeAsync_WhenLive_DoesNotThrow()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        Task task = viewManager.ToggleJsonArrayModeAsync();

        // Assert
        await task;
        task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleJsonArrayModeAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.Dispose();

        // Act
        Func<Task> act = () => viewManager.ToggleJsonArrayModeAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void SwitchToJsonArrayTree_WithValidIndexer_SetsCurrentView()
    {
        // Arrange
        string filePath = _tempFiles.Create(".json", "[1,2,3]");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        viewManager.SwitchToJsonArrayTree(indexer);

        // Assert
        viewManager.GetCurrentView().Should().BeOfType<JsonArrayTreeView>();
    }

    [Fact]
    public void SwitchToJsonArrayTree_WithValidIndexer_DoesNotShowTotalCountBeforeIndexCompletion()
    {
        // Arrange
        string filePath = _tempFiles.Create(".json", "[1,2,3]");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        state.EnterJsonArrayTree();
        using Window window = new();
        using StatusBar statusBar = new();
        window.Add(statusBar);
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        RowIndexer indexer = new(filePath);

        // Act — do NOT call BuildIndex, so IsIndexingCompleted stays false
        viewManager.SwitchToJsonArrayTree(indexer);

        // Assert — item count is not shown until BuildIndexCompleted fires
        StatusBar? currentStatusBar = viewManager.GetCurrentStatusBar();
        currentStatusBar.Should().NotBeNull();
        IEnumerable<string> hints = Enumerable.Select(
                Enumerable.OfType<Shortcut>(currentStatusBar.SubViews),
                s => s.HelpText);
        hints.Should().NotContainMatch("*items*");
        window.SubViews.OfType<Label>().Should().NotContain(l => l.Text.Contains("items", StringComparison.Ordinal));
    }

    [Fact]
    public void SwitchToJsonArrayTree_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        IRowIndexer? nullIndexer = null;

        // Act
        Action act = () => viewManager.SwitchToJsonArrayTree(nullIndexer!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SwitchToJsonArrayTree_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.Dispose();

        // Act
        Action act = () => viewManager.SwitchToJsonArrayTree(new CompletedStubRowIndexer("test.json"));

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void SwitchToJsonObjectTree_WithValidEntries_SetsCurrentView()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile("test.json");
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        IReadOnlyList<JsonObjectEntry> entries =
        [
            new JsonObjectEntry("id", System.Text.Encoding.UTF8.GetBytes("1")),
        ];

        // Act
        viewManager.SwitchToJsonObjectTree(entries);

        // Assert
        viewManager.GetCurrentView().Should().BeOfType<JsonObjectTreeView>();
    }

    [Fact]
    public void SwitchToJsonObjectTree_WithNullEntries_ThrowsArgumentNullException()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        IReadOnlyList<JsonObjectEntry>? nullEntries = null;

        // Act
        Action act = () => viewManager.SwitchToJsonObjectTree(nullEntries!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SwitchToJsonObjectTree_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.Dispose();
        IReadOnlyList<JsonObjectEntry> entries = [];

        // Act
        Action act = () => viewManager.SwitchToJsonObjectTree(entries);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WhenScanSucceeds_SwitchesToFocusedTable()
    {
        // Arrange — real file + matching KeyPath; ModeController is sealed and can't be mocked, so
        // success is driven by an actual scan over an actual file.
        string filePath = _tempFiles.Create(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        FullAggregationDrillDownRequest request = new(
            DataFormat.JsonLines,
            [new KeyPathSegment("user", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act — immediate-execution uiThreadInvoke applies the scanned result synchronously
        await viewManager.FullAggregationDrillDownAsync(request);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        state.GetDrillDownOrNull().Should().NotBeNull();
        viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>();
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WhenScanFails_ShowsErrorPlaceholder()
    {
        // Arrange — real file + non-matching KeyPath, so the sealed ModeController's scan fails.
        string filePath = _tempFiles.Create(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        FullAggregationDrillDownRequest request = new(
            DataFormat.JsonLines,
            [new KeyPathSegment("missing", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        await viewManager.FullAggregationDrillDownAsync(request);

        // Assert — failure routes through ShowError into a PlaceholderView carrying the message
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        viewManager.GetCurrentView().Should().BeOfType<PlaceholderView>().Which.Text.Should().Be("No matching records found.");
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WithDeferredUiThreadInvoke_DoesNotMutateStateUntilCallback()
    {
        // Arrange — inject a deferred-capture uiThreadInvoke so state changes only happen once the
        // captured callback runs, mirroring how a real TUI posts work to the UI thread.
        string filePath = _tempFiles.Create(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);

        List<Action> capturedCallbacks = [];
        using ViewManager viewManager = new(
            window, state, modeController, new FilterIndexingRunner(), cb => capturedCallbacks.Add(cb));

        FullAggregationDrillDownRequest request = new(
            DataFormat.JsonLines,
            [new KeyPathSegment("user", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act — the background scan completes; the callback is captured but NOT yet executed
        await viewManager.FullAggregationDrillDownAsync(request);

        // Assert — phase 1: state must be untouched before the UI-thread callback runs
        capturedCallbacks.Should().ContainSingle();
        state.GetDrillDownOrNull().Should().BeNull();
        state.CurrentMode.Should().Be(ViewMode.FileSelection);

        // Act — dispatch the captured callback (what the real UI thread would run)
        capturedCallbacks[0].Invoke();

        // Assert — phase 2: only now does the successful result reach AppState and switch the view
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        state.GetDrillDownOrNull().Should().NotBeNull();
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WhenViewManagerDisposedBeforeCallback_Throws()
    {
        // Arrange — a success scan is required to reach SwitchToFocusedTable's dispose guard;
        // dispose before invoking the captured callback to simulate the race.
        string filePath = _tempFiles.Create(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);

        List<Action> capturedCallbacks = [];
        // Not 'using' — disposed manually below to simulate the race between scan completion and dispatch.
        ViewManager viewManager = new(
            window, state, modeController, new FilterIndexingRunner(), cb => capturedCallbacks.Add(cb));

        FullAggregationDrillDownRequest request = new(
            DataFormat.JsonLines,
            [new KeyPathSegment("user", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act — scan completes and the callback is captured; dispose BEFORE the callback executes
        await viewManager.FullAggregationDrillDownAsync(request);
        viewManager.Dispose();

        // Assert — the fail-fast guard throws before any state mutation.
        capturedCallbacks.Should().ContainSingle();
        Action act = () => capturedCallbacks[0].Invoke();
        act.Should().Throw<ObjectDisposedException>();
        state.GetDrillDownOrNull().Should().BeNull();
        state.CurrentMode.Should().Be(ViewMode.FileSelection);
    }

    [Fact]
    public void SwitchToCsvTable_WithStaleKeyPath_ResetsCurrentKeyPath()
    {
        // Arrange — a leftover KeyPath from a prior Tree/FocusedTable session must not leak into CsvTable
        string filePath = _tempFiles.Create(".csv", "col1\nvalue1\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        state.SetCurrentKeyPath([new KeyPathSegment("stale", KeyPathSegmentKind.Key)]);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.Csv,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };

        // Act
        viewManager.SwitchToCsvTable(new CompletedStubRowIndexer(filePath), schema);

        // Assert
        state.CurrentKeyPath.Should().BeEmpty();
        window.SubViews.OfType<BreadcrumbBar>().Single().Text.Should().BeEmpty();
    }

    [Fact]
    public void SwitchToCsvTable_WhenDisplayedRowsHaveMismatchedColumnCount_ShowsErrorViewOnceAfterDraw()
    {
        // Arrange
        using RaggedCsvFixture fixture = RaggedCsvFixture.Create(_tempFiles.Create(".csv", "col1,col2\nval1,val2\nval3\nval4,val5\n"));
        int queuedBeforeRead = fixture.Queued.Count;
        CsvTableView csvView = fixture.ViewManager.GetCurrentView().Should().BeOfType<CsvTableView>().Subject;
        csvView.Table.Should().NotBeNull();

        // Act - drawing reads cells of the ragged window; the error must not swap views mid-draw
        _ = csvView.Table[0, 0];
        _ = csvView.Table[1, 1];
        Terminal.Gui.ViewBase.View? viewDuringDraw = fixture.ViewManager.GetCurrentView();
        Action callback = fixture.Queued.Skip(queuedBeforeRead).ToList().Should().ContainSingle().Subject;
        callback();

        // Assert
        viewDuringDraw.Should().BeSameAs(csvView);
        fixture.State.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        Terminal.Gui.ViewBase.View? errorView = fixture.ViewManager.GetCurrentView();
        errorView.Should().NotBeNull();
        errorView.Text.Should().Contain("Error reading CSV");
    }

    [Fact]
    public void SwitchToCsvTable_WhenModeChangedBeforeQueuedErrorRuns_KeepsCurrentView()
    {
        // Arrange
        using RaggedCsvFixture fixture = RaggedCsvFixture.Create(_tempFiles.Create(".csv", "col1,col2\nval1\n"));
        int queuedBeforeRead = fixture.Queued.Count;
        CsvTableView csvView = fixture.ViewManager.GetCurrentView().Should().BeOfType<CsvTableView>().Subject;
        csvView.Table.Should().NotBeNull();
        _ = csvView.Table[0, 0];
        Action callback = fixture.Queued.Skip(queuedBeforeRead).ToList().Should().ContainSingle().Subject;
        fixture.State.EnterPlaceholderMode();

        // Act
        callback();

        // Assert
        fixture.ViewManager.GetCurrentView().Should().BeSameAs(csvView);
    }

    [Fact]
    public void SwitchToCsvTable_WhenViewReplacedByAnotherCsvViewBeforeQueuedErrorRuns_KeepsNewView()
    {
        // Arrange
        using RaggedCsvFixture fixture = RaggedCsvFixture.Create(_tempFiles.Create(".csv", "col1,col2\nval1\n"));
        int queuedBeforeRead = fixture.Queued.Count;
        CsvTableView oldView = fixture.ViewManager.GetCurrentView().Should().BeOfType<CsvTableView>().Subject;
        oldView.Table.Should().NotBeNull();
        _ = oldView.Table[0, 0];
        Action callback = fixture.Queued.Skip(queuedBeforeRead).ToList().Should().ContainSingle().Subject;
        fixture.ViewManager.SwitchToCsvTable(fixture.Indexer, fixture.Schema);
        Terminal.Gui.ViewBase.View? newView = fixture.ViewManager.GetCurrentView();

        // Act
        callback();

        // Assert
        newView.Should().BeOfType<CsvTableView>().And.NotBeSameAs(oldView);
        fixture.ViewManager.GetCurrentView().Should().BeSameAs(newView);
        fixture.State.CurrentMode.Should().Be(ViewMode.CsvTable);
    }

    [Fact]
    public void SwitchToCsvTable_WhenNewFileSessionStartsBeforeQueuedErrorRuns_KeepsCurrentSession()
    {
        // Arrange - the old view is still current while the next file loads
        using RaggedCsvFixture fixture = RaggedCsvFixture.Create(_tempFiles.Create(".csv", "col1,col2\nval1\n"));
        int queuedBeforeRead = fixture.Queued.Count;
        CsvTableView csvView = fixture.ViewManager.GetCurrentView().Should().BeOfType<CsvTableView>().Subject;
        csvView.Table.Should().NotBeNull();
        _ = csvView.Table[0, 0];
        Action callback = fixture.Queued.Skip(queuedBeforeRead).ToList().Should().ContainSingle().Subject;
        string nextFilePath = _tempFiles.Create(".csv", "col1,col2\nval1,val2\n");
        fixture.State.StartNewFile(nextFilePath);

        // Act
        callback();

        // Assert
        fixture.State.CurrentMode.Should().Be(ViewMode.CsvTable);
        fixture.State.CurrentFilePath.Should().Be(nextFilePath);
        fixture.ViewManager.GetCurrentView().Should().BeSameAs(csvView);
    }

    [Fact]
    public void SwitchToCsvTable_WhenViewManagerDisposedBeforeQueuedErrorRuns_DoesNothing()
    {
        // Arrange
        using RaggedCsvFixture fixture = RaggedCsvFixture.Create(_tempFiles.Create(".csv", "col1,col2\nval1\n"));
        int queuedBeforeRead = fixture.Queued.Count;
        CsvTableView csvView = fixture.ViewManager.GetCurrentView().Should().BeOfType<CsvTableView>().Subject;
        csvView.Table.Should().NotBeNull();
        _ = csvView.Table[0, 0];
        Action callback = fixture.Queued.Skip(queuedBeforeRead).ToList().Should().ContainSingle().Subject;
        fixture.ViewManager.Dispose();

        // Act
        Action act = () => callback();

        // Assert
        act.Should().NotThrow();
        fixture.State.CurrentMode.Should().Be(ViewMode.CsvTable);
    }

    [Fact]
    public void SwitchToJsonLinesTableView_WithStaleKeyPath_ResetsCurrentKeyPath()
    {
        // Arrange — a leftover KeyPath from a prior Tree/FocusedTable session must not leak into JsonLinesTable
        string filePath = _tempFiles.Create(".jsonl", "{\"col1\": \"value\"}\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        state.SetCurrentKeyPath([new KeyPathSegment("stale", KeyPathSegmentKind.Key)]);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };

        // Act
        viewManager.SwitchToJsonLinesTableView(new CompletedStubRowIndexer(filePath), schema);

        // Assert
        state.CurrentKeyPath.Should().BeEmpty();
        window.SubViews.OfType<BreadcrumbBar>().Single().Text.Should().BeEmpty();
    }

    [Fact]
    public void SwitchToJsonLinesTableView_WithFilterAction_StartsFilterScanWithIndexerAndStateToken()
    {
        // Arrange
        string filePath = _tempFiles.Create(".jsonl", "{\"col1\": \"value\"}\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        state.AddMorphAction(FilterAction.Create("col1", FilterOperator.Equals, ComparisonType.Text, "value").Value);
        using Window window = new();
        CapturingFilterScanRunner runner = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, runner, action => action());
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };

        // Act
        viewManager.SwitchToJsonLinesTableView(new CompletedStubRowIndexer(filePath), schema);

        // Assert
        CapturingFilterScanRunner.Call call = runner.Calls.Should().ContainSingle().Subject;
        call.Indexer.Should().BeOfType<Refedle.Engine.IO.JsonLines.FilterRowIndexer>();
        call.Token.Should().Be(state.Cts.Token);
    }

    [Fact]
    public void SwitchToJsonLinesTableView_WithoutActions_DoesNotStartFilterScan()
    {
        // Arrange
        string filePath = _tempFiles.Create(".jsonl", "{\"col1\": \"value\"}\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        using Window window = new();
        CapturingFilterScanRunner runner = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, runner, action => action());
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };

        // Act
        viewManager.SwitchToJsonLinesTableView(new CompletedStubRowIndexer(filePath), schema);

        // Assert
        runner.Calls.Should().BeEmpty();
    }

    [Fact]
    public void SchemaRefined_WithActionsApplied_KeepsViewAndDisplaysNewColumnValue()
    {
        // Arrange
        using SchemaRefinementHarness harness = new(
            [new RenameColumnAction { OldName = "col1", NewName = "renamed" }],
            "col1");
        ITableSource? oldTable = harness.View.Table;

        // Act
        harness.Refine("col1", "col2");

        // Assert
        harness.ViewManager.GetCurrentView().Should().BeSameAs(harness.View);
        harness.View.Table.Should().NotBeSameAs(oldTable);
        harness.Table.ColumnNames.Should().Equal("renamed (text)", "col2 (text)");
        harness.Table[0, 0].Should().Be("a");
        harness.Table[0, 1].Should().Be("b");
    }

    [Fact]
    public async Task SchemaRefined_WhileFilterScanPaused_ShowsFilteredRowsAfterResumeWithoutSecondScan()
    {
        // Arrange
        using SchemaRefinementHarness harness = new(
            [FilterAction.Create("col1", FilterOperator.Equals, ComparisonType.Text, "a").Value],
            "col1");
        ITableSource? oldTable = harness.View.Table;
        CapturingFilterScanRunner.Call scan = harness.Runner.Calls.Should().ContainSingle().Subject;

        // Act
        harness.Refine("col1", "col2");
        harness.Runner.Resume();
        await harness.Runner.Completion;

        // Assert
        harness.View.Table.Should().NotBeSameAs(oldTable);
        harness.Runner.Calls.Should().ContainSingle().Which.Should().BeSameAs(scan);
        GetLazyTransformer(harness.ViewManager).FilterRowIndexer.Should().BeSameAs(scan.Indexer);
        harness.Table.Rows.Should().Be(1);
        harness.Table[0, 1].Should().Be("b");
    }

    [Fact]
    public void SchemaRefined_AfterViewReplaced_LeavesCurrentViewUntouched()
    {
        // Arrange
        using SchemaRefinementHarness harness = new(
            [new RenameColumnAction { OldName = "col1", NewName = "renamed" }],
            "col1");
        Action<TableSchema> staleCallback = harness.State.OnSchemaRefined.Should().BeOfType<Action<TableSchema>>().Subject;
        harness.ViewManager.SwitchToFileSelection();
        Terminal.Gui.ViewBase.View? currentView = harness.ViewManager.GetCurrentView();

        // Act
        Exception exception = Record.Exception(() => staleCallback(CreateJsonLinesSchema("col1", "col2")));

        // Assert
        exception.Should().BeNull();
        harness.ViewManager.GetCurrentView().Should().BeSameAs(currentView);
        currentView.Should().BeOfType<FileSelectionView>();
    }

    [Fact]
    public void SchemaRefined_WithoutActions_ExposesNewColumnOnSameView()
    {
        // Arrange
        using SchemaRefinementHarness harness = new([], "col1");

        // Act
        harness.Refine("col1", "col2");

        // Assert
        harness.ViewManager.GetCurrentView().Should().BeSameAs(harness.View);
        harness.Table.ColumnNames.Should().Equal("col1 (text)", "col2 (text)");
        harness.Table[0, 1].Should().Be("b");
        harness.Runner.Calls.Should().BeEmpty();
    }

    private static LazyTransformer GetLazyTransformer(ViewManager viewManager)
    {
        JsonLinesTableView view = viewManager.GetCurrentView().Should().BeOfType<JsonLinesTableView>().Subject;
        PagedTableSource paged = view.Table.Should().BeOfType<PagedTableSource>().Subject;
        ColumnWidthStabilizingTableSource stabilizing = paged.Inner.Should().BeOfType<ColumnWidthStabilizingTableSource>().Subject;
        return stabilizing.Inner.Should().BeOfType<LazyTransformer>().Subject;
    }

    [Fact]
    public void SwitchToFileSelection_WithStaleKeyPath_ResetsCurrentKeyPath()
    {
        // Arrange — a leftover KeyPath from a prior Tree/FocusedTable session must not leak into FileSelection
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.SetCurrentKeyPath([new KeyPathSegment("stale", KeyPathSegmentKind.Key)]);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.SwitchToFileSelection();

        // Assert
        state.CurrentKeyPath.Should().BeEmpty();
        window.SubViews.OfType<BreadcrumbBar>().Single().Text.Should().BeEmpty();
    }

    [Fact]
    public void DrillDown_WithIndexSegmentInKeyPath_RendersLiteralIndexInBreadcrumb()
    {
        // Arrange — SingleDrillDownRequest (Phase 1) must not collapse array indices
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        SingleDrillDownRequest request = new(
            DataFormat.JsonObject,
            Encoding.UTF8.GetBytes("[{\"a\":1}]"),
            [
                new KeyPathSegment("list", KeyPathSegmentKind.Key),
                new KeyPathSegment("[0]", KeyPathSegmentKind.Index),
            ],
            InitialActionStack: []);

        // Act
        viewManager.DrillDown(request);

        // Assert
        window.SubViews.OfType<BreadcrumbBar>().Single().Text.Should().Be("list[0]");
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WithIndexSegmentInKeyPath_RendersCollapsedIndexInBreadcrumb()
    {
        // Arrange — FullAggregationDrillDownRequest (Phase 2) collapses array indices to [*]
        string filePath = _tempFiles.Create(".jsonl", "{\"list\":[{\"a\":1}]}\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        FullAggregationDrillDownRequest request = new(
            DataFormat.JsonLines,
            [
                new KeyPathSegment("list", KeyPathSegmentKind.Key),
                new KeyPathSegment("[0]", KeyPathSegmentKind.Index),
            ],
            InitialActionStack: []);

        // Act
        await viewManager.FullAggregationDrillDownAsync(request);

        // Assert
        window.SubViews.OfType<BreadcrumbBar>().Single().Text.Should().Be("list[*]");
    }

    [Fact]
    public void SwitchToFocusedTable_WithEmptyActionStack_SetsMorphCallbacksOnRawSource()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        DrillDownState drillDown = new(
            [new FocusedTableRow(Encoding.UTF8.GetBytes("{\"name\":\"Alice\"}"), "[0]")],
            schema,
            ViewMode.JsonArrayTree,
            KeyPath: [],
            ActionStack: []);
        using AppState state = new();
        state.EnterFocusedTable(drillDown);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.SwitchToFocusedTable(drillDown);

        // Assert — callbacks are wired even with no actions, so Morph works from the first DrillDown
        FocusedTableView view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        ColumnWidthStabilizingTableSource decorated = view.Table.Should().BeOfType<PagedTableSource>().Which
            .Inner.Should().BeOfType<ColumnWidthStabilizingTableSource>().Which;
        decorated.Inner.Should().BeOfType<FocusedTableSource>();
        view.IsRowIndexComplete.Should().BeNull();
        view.OnMorphAction.Should().NotBeNull();
        decorated.RawColumnNames[1].Should().Be("name");
    }

    [Fact]
    public void RefreshCurrentTableView_WithFocusedTableModeAndDrillDown_RendersThroughFocusedTableTransformer()
    {
        // Arrange — a pending action proves the re-render went through SwitchToFocusedTable's wrap path
        using IApplication app = TestAppFactory.Create();
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        DrillDownState drillDown = new(
            [new FocusedTableRow(Encoding.UTF8.GetBytes("{\"name\":\"Alice\"}"), "[0]")],
            schema,
            ViewMode.JsonArrayTree,
            KeyPath: [],
            ActionStack: [new RenameColumnAction { OldName = "name", NewName = "label" }]);
        using AppState state = new();
        state.EnterFocusedTable(drillDown);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.RefreshCurrentTableView();

        // Assert
        FocusedTableView view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which
            .Inner.Should().BeOfType<ColumnWidthStabilizingTableSource>().Which.Inner.Should().BeOfType<FocusedTableTransformer>();
        view.Table.ColumnNames.Should().Contain("label (text)");
    }

    private static async Task<Action> WaitForCallbackAsync(System.Collections.Concurrent.ConcurrentQueue<Action> captured)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        Action? callback = null;
        while (!captured.TryDequeue(out callback) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        return callback ?? throw new TimeoutException("The filter scan did not produce a UI callback.");
    }

    private static void ExecuteRemainingCallbacks(System.Collections.Concurrent.ConcurrentQueue<Action> captured)
    {
        while (captured.TryDequeue(out Action? callback))
        {
            callback();
        }
    }

    private static async Task<FocusedTableView> WaitForFocusedTableViewAsync(
        ViewManager viewManager,
        System.Collections.Concurrent.ConcurrentQueue<Action> captured)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (viewManager.GetCurrentView() is not FocusedTableView && DateTime.UtcNow < deadline)
        {
            bool executedAny = false;
            while (captured.TryDequeue(out Action? callback))
            {
                callback();
                executedAny = true;
            }

            if (!executedAny)
            {
                await Task.Delay(10);
            }
        }

        return viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
    }

    private static void AssertAtFirstPageAndRow(FocusedTableView view)
    {
        PagedTableSource paged = view.Table.Should().BeOfType<PagedTableSource>().Which;
        paged.CurrentPageNumber.Should().Be(1);
        paged.WindowStart.Should().Be(0);
        view.Value.Should().NotBeNull().And.Subject.As<TableSelection>().SelectedCell.Y.Should().Be(0);
        view.RowOffset.Should().Be(0);
    }

    /// <summary>
    /// Waits until the captured invoke queue holds <paramref name="expectedCount"/> callbacks
    /// without executing any of them, so their effect stays pending for the test to apply.
    /// </summary>
    private static async Task WaitForCallbacksAsync(
        System.Collections.Concurrent.ConcurrentQueue<Action> captured,
        int expectedCount)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (captured.Count < expectedCount && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        captured.Count.Should().BeGreaterThanOrEqualTo(expectedCount);
    }

    private static FocusedTableTransformer GetFocusedTableTransformer(FocusedTableView view) =>
        view.Table.Should().BeOfType<PagedTableSource>().Which
            .Inner.Should().BeOfType<ColumnWidthStabilizingTableSource>().Which
            .Inner.Should().BeOfType<FocusedTableTransformer>().Subject;

    /// <summary>
    /// DrillDown whose first <paramref name="matchedRows"/> rows match a "Match" filter and
    /// whose read of the very next row faults, so the background scan fails deterministically
    /// mid-batch — before the reveal threshold when <paramref name="matchedRows"/> is below
    /// it, after the threshold otherwise.
    /// </summary>
    private static DrillDownState CreateFaultingDrillDown(int matchedRows)
    {
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        IEnumerable<string> names = Enumerable.Range(0, matchedRows).Select(static i => $"Match{i}")
            .Concat(Enumerable.Range(0, 1000 - matchedRows).Select(static i => $"Other{i}"));
        FocusedTableRow[] rows = [.. names.Select(static (name, index) => new FocusedTableRow(Encoding.UTF8.GetBytes($"{{\"name\":\"{name}\"}}"), $"[{index}]"))];
        return new DrillDownState(new FaultingDrillDownRows(rows, faultRowIndex: matchedRows), schema, ViewMode.JsonArrayTree, KeyPath: [], ActionStack: []);
    }

    /// <summary>
    /// Executes queued callbacks until the current view is a FocusedTable view whose scan
    /// overlay is gone — the terminal look of a completed staged scan, regardless of how many
    /// notifications the scan produced.
    /// </summary>
    private static List<Label> OverlayLabels(Terminal.Gui.ViewBase.View root) =>
        [.. SelfAndDescendants(root).Where(IsProgressOverlayLabel).OfType<Label>()];

    private static async Task<FocusedTableView> WaitForCompletedFocusedTableViewAsync(
        ViewManager viewManager,
        System.Collections.Concurrent.ConcurrentQueue<Action> captured,
        Terminal.Gui.ViewBase.View root)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            ExecuteRemainingCallbacks(captured);
            if (viewManager.GetCurrentView() is FocusedTableView { } view && OverlayLabels(root).Count == 0)
            {
                return view;
            }

            await Task.Delay(10);
        }

        // failing beats returning the current view: a scan that never reached its terminal
        // look must not pass the test through a fallback value
        throw new TimeoutException("The filter scan did not reach its completed table view.");
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFilterAndFewMatches_ShowsLoadingViewThenTableOnCompletion()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Alice3").Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using AppState state = new();
        state.EnterFocusedTable(drillDown);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        ConcurrentQueue<Action> captured = new();
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), captured.Enqueue);

        // Act
        viewManager.SwitchToFocusedTable(drillDown);

        // Assert — one match is below the display threshold, so the loading view stays
        viewManager.GetCurrentView().Should().BeOfType<LoadingView>();
        Action onProgress = await WaitForCallbackAsync(captured);
        onProgress();
        viewManager.GetCurrentView().Should().BeOfType<LoadingView>();

        Action onCompleted = await WaitForCallbackAsync(captured);
        onCompleted();

        FocusedTableView view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(1);
        OverlayLabels(window).Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFilterMatchingManyRows_RevealsTableFromThresholdNotification()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 500) with { ActionStack = [filter] };
        using AppState state = new();
        state.EnterFocusedTable(drillDown);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        ConcurrentQueue<Action> captured = new();
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), captured.Enqueue);

        // Act
        viewManager.SwitchToFocusedTable(drillDown);
        viewManager.GetCurrentView().Should().BeOfType<LoadingView>();

        // Assert — 500 matched rows exceed the threshold, so the reveal and the first tick
        // (queued in either order) both stage the table; only the completion stays pending
        await WaitForCallbacksAsync(captured, expectedCount: 3);
        Action onReveal = await WaitForCallbackAsync(captured);
        onReveal();
        Action onFirstTick = await WaitForCallbackAsync(captured);
        onFirstTick();

        FocusedTableView view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(500);
        OverlayLabels(window).Should().ContainSingle();

        Action onCompleted = await WaitForCallbackAsync(captured);
        onCompleted();
        OverlayLabels(window).Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFilterAndCancelledToken_StaysOnLoadingViewWithoutCallbacks()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);
        harness.State.Cts.Cancel();

        // Act
        harness.ViewManager.SwitchToFocusedTable(drillDown);

        // Assert — Task.Run with an already-canceled token never runs the scan delegate and
        // the await completes without a completion callback, so no delay is needed.
        harness.ViewManager.GetCurrentView().Should().BeOfType<LoadingView>();
        harness.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFilterThenViewSwitch_StaleCallbacksDoNotRestoreFocusedTable()
    {
        // Arrange — one match: its tick and completion queue up while the loading view stays
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);

        // Act — switching views cancels the scan before any callback ran
        harness.ViewManager.SwitchToFileSelection();

        // Assert — executing the stale tick and completion cannot resurrect the scan's views
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        harness.ViewManager.GetCurrentView().Should().BeOfType<FileSelectionView>();
    }

    [Fact]
    public async Task HandleMorphAction_WithFilterAfterMovingAwayFromFirstRow_ShowsTableAtFirstPageAndRow()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 12);
        using AppState state = new();
        state.EnterFocusedTable(drillDown);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        ConcurrentQueue<Action> captured = new();
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), captured.Enqueue);
        viewManager.SwitchToFocusedTable(drillDown);
        captured.Clear();
        FocusedTableView viewBeforeFilter = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        viewBeforeFilter.SetSelection(0, 5, false);
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;

        // Act
        viewBeforeFilter.OnMorphAction?.Invoke(filter);
        FocusedTableView viewAfterFilter = await WaitForFocusedTableViewAsync(viewManager, captured);
        ExecuteRemainingCallbacks(captured);

        // Assert
        viewAfterFilter.Should().NotBeSameAs(viewBeforeFilter);
        AssertAtFirstPageAndRow(viewAfterFilter);
    }

    [Fact]
    public async Task RefreshCurrentTableView_AfterClearingFilterFromMovedSelection_ReturnsToFirstPageAndRow()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        DrillDownState baseDrillDown = CreateNumberedDrillDown(rowCount: 12);
        DrillDownState drillDown = baseDrillDown with { ActionStack = [filter] };
        using AppState state = new();
        state.EnterFocusedTable(drillDown);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        ConcurrentQueue<Action> captured = new();
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), captured.Enqueue);
        viewManager.SwitchToFocusedTable(drillDown);
        FocusedTableView viewBeforeClear = await WaitForFocusedTableViewAsync(viewManager, captured);
        ExecuteRemainingCallbacks(captured);
        viewBeforeClear.SetSelection(0, 5, false);
        state.ClearDrillDownActions();

        // Act
        viewManager.RefreshCurrentTableView();

        // Assert
        FocusedTableView viewAfterClear = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        viewAfterClear.Should().NotBeSameAs(viewBeforeClear);
        AssertAtFirstPageAndRow(viewAfterClear);
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFewerMatchesThanThresholdAndQueuedCallbacks_StaysOnLoadingViewUntilCompletionCallback()
    {
        // Arrange — 199 matches; the worker finishes before any queued callback runs (not a mid-scan test)
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = CreateMixedDrillDown(matchedInFirstBatch: 199, matchedInSecondBatch: 0)
            with
        { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);

        // Act — two batches queue two ticks (first + final) plus the completion
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 3);

        // Assert — neither tick reaches the display threshold, so the loading view stays
        Action onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        harness.ViewManager.GetCurrentView().Should().BeOfType<LoadingView>();
        Action onFinalTick = await WaitForCallbackAsync(harness.Captured);
        onFinalTick();
        harness.ViewManager.GetCurrentView().Should().BeOfType<LoadingView>();

        Action onCompleted = await WaitForCallbackAsync(harness.Captured);
        onCompleted();

        FocusedTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(199);
        OverlayLabels(harness.Window).Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithThresholdMatchesAndQueuedCallbacks_RevealsTableFromThresholdAndGrowsRows()
    {
        // Arrange — 200 + 50 matches; callbacks are applied after the worker finished (not a mid-scan test)
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = CreateMixedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 50)
            with
        { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);

        // Act — the threshold reveal and both ticks queue (in any mutual order) before the completion
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 4);
        Action onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        Action onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        Action onFinalTick = await WaitForCallbackAsync(harness.Captured);
        onFinalTick();

        // Assert — executing every pre-completion callback leaves the table staged with the
        // overlay attached; the live matched count by deferred-execution time is the final 250
        FocusedTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(250);
        Label overlayLabel = OverlayLabels(harness.Window).Should().ContainSingle().Subject;
        overlayLabel.SuperView.Should().BeSameAs(view.SuperView, "the overlay rides the content container, not the scrolling table");
        SelfAndDescendants(view).Where(IsProgressOverlayLabel).Should().BeEmpty();
        List<Terminal.Gui.ViewBase.View> drawOrder = [.. (view.SuperView?.SubViews ?? [])];
        drawOrder.IndexOf(overlayLabel).Should().BeGreaterThan(drawOrder.IndexOf(view), "the table must not draw over the overlay");
        AssertAtFirstPageAndRow(view);
        FocusedTableTransformer transformer = GetFocusedTableTransformer(view);

        Action onCompleted = await WaitForCallbackAsync(harness.Captured);
        onCompleted();
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(250);
        OverlayLabels(harness.Window).Should().BeEmpty();

        // the completed path hands the transformer to the table view: it stays readable
        transformer[0, 1].Should().Be("Match0");
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithMoreMatchesThanThresholdAndQueuedCallbacks_RevealsTableFromQueuedCallbacks()
    {
        // Arrange — 201 matches; callbacks are applied after the worker finished (not a mid-scan test)
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = CreateMixedDrillDown(matchedInFirstBatch: 201, matchedInSecondBatch: 0)
            with
        { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);

        // Act — the threshold reveal and both ticks queue (in any mutual order) before the completion
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 4);
        Action onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        Action onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        Action onFinalTick = await WaitForCallbackAsync(harness.Captured);
        onFinalTick();

        // Assert — executing every pre-completion callback reveals the table
        FocusedTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(201);
        Label overlayLabel = OverlayLabels(harness.Window).Should().ContainSingle().Subject;
        overlayLabel.SuperView.Should().BeSameAs(view.SuperView, "the overlay rides the content container, not the scrolling table");
        SelfAndDescendants(view).Where(IsProgressOverlayLabel).Should().BeEmpty();
        List<Terminal.Gui.ViewBase.View> drawOrder = [.. (view.SuperView?.SubViews ?? [])];
        drawOrder.IndexOf(overlayLabel).Should().BeGreaterThan(drawOrder.IndexOf(view), "the table must not draw over the overlay");
        AssertAtFirstPageAndRow(view);

        Action onCompleted = await WaitForCallbackAsync(harness.Captured);
        onCompleted();
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(201);
        OverlayLabels(harness.Window).Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFilter_AppliesWorkerNotificationsOnlyThroughUiThreadInvoke()
    {
        // Arrange — two batches: one threshold reveal plus two ticks plus one completion,
        // all queued by the worker
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = CreateMixedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 50)
            with
        { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);

        // Act
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        LoadingView loadingView = harness.ViewManager.GetCurrentView().Should().BeOfType<LoadingView>().Which;
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 4);

        // Assert — nothing the worker fired touches the view state before the UI thread invokes it
        harness.ViewManager.GetCurrentView().Should().BeSameAs(loadingView);
        OverlayLabels(harness.Window).Should().ContainSingle();

        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();

        FocusedTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(250);
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFewerMatchesThanThresholdWhileScanRunning_StaysOnLoadingView()
    {
        // Arrange — 199 matches in the first batch; the worker blocks on the second batch's first row
        using IApplication app = TestAppFactory.Create();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = CreateGatedDrillDown(matchedInFirstBatch: 199, matchedInSecondBatch: 0, totalBatches: 2);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        await using GatedScanScope scope = new(rows, harness);
        harness.State.EnterFocusedTable(drillDown);

        // Act — the first tick is queued while the worker is provably mid-scan
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Action onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();

        // Assert — 199 matches stay below the display threshold while the scan runs
        harness.ViewManager.GetCurrentView().Should().BeOfType<LoadingView>();

        rows.ReleaseGate();
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);
        FocusedTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(199);
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithThresholdMatchesWhileScanRunning_RevealsTableAndGrowsRowsKeepingUserSelection()
    {
        // Arrange — 200 matches in the first batch, 50 more in the second; the worker blocks
        // on the second batch's first row
        using IApplication app = TestAppFactory.Create();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 50, totalBatches: 2);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        await using GatedScanScope scope = new(rows, harness);
        harness.State.EnterFocusedTable(drillDown);

        // Act — once the worker is blocked, exactly two batch-1 notifications are queued:
        // the threshold reveal and the first tick, in whichever order the threads landed
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);

        // Assert — the mid-scan reveal shows exactly the threshold rows with the overlay attached
        FocusedTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(200);
        Label overlayLabel = OverlayLabels(harness.Window).Should().ContainSingle().Subject;
        overlayLabel.SuperView.Should().BeSameAs(view.SuperView, "the overlay rides the content container, not the scrolling table");
        SelfAndDescendants(view).Where(IsProgressOverlayLabel).Should().BeEmpty();
        List<Terminal.Gui.ViewBase.View> drawOrder = [.. (view.SuperView?.SubViews ?? [])];
        drawOrder.IndexOf(overlayLabel).Should().BeGreaterThan(drawOrder.IndexOf(view), "the table must not draw over the overlay");
        AssertAtFirstPageAndRow(view);

        // the user moves off the first row, then the scan resumes and finds 50 more matches;
        // every remaining notification is applied while the completed look is awaited
        view.SetSelection(0, 5, false);
        rows.ReleaseGate();
        await WaitForCompletedFocusedTableViewAsync(harness.ViewManager, harness.Captured, harness.Window);

        // rows grew to 250 without moving the user's selection, and the scan retired the overlay
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(250);
        view.Value.Should().NotBeNull().And.Subject.As<TableSelection>().SelectedCell.Y.Should().Be(5);
        OverlayLabels(harness.Window).Should().BeEmpty();
        harness.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithMoreMatchesThanThresholdWhileScanRunning_RevealsTableMidScan()
    {
        // Arrange — 201 matches in the first batch, none in the second; the worker blocks on
        // the second batch's first row
        using IApplication app = TestAppFactory.Create();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = CreateGatedDrillDown(matchedInFirstBatch: 201, matchedInSecondBatch: 0, totalBatches: 2);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        await using GatedScanScope scope = new(rows, harness);
        harness.State.EnterFocusedTable(drillDown);

        // Act — once the worker is blocked, exactly two batch-1 notifications are queued:
        // the threshold reveal and the first tick, in whichever order the threads landed
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);

        // Assert — the mid-scan reveal shows the 201 matched rows with the overlay attached
        FocusedTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(201);
        Label overlayLabel = OverlayLabels(harness.Window).Should().ContainSingle().Subject;
        overlayLabel.SuperView.Should().BeSameAs(view.SuperView, "the overlay rides the content container, not the scrolling table");
        SelfAndDescendants(view).Where(IsProgressOverlayLabel).Should().BeEmpty();
        List<Terminal.Gui.ViewBase.View> drawOrder = [.. (view.SuperView?.SubViews ?? [])];
        drawOrder.IndexOf(overlayLabel).Should().BeGreaterThan(drawOrder.IndexOf(view), "the table must not draw over the overlay");
        AssertAtFirstPageAndRow(view);

        rows.ReleaseGate();
        await WaitForCompletedFocusedTableViewAsync(harness.ViewManager, harness.Captured, harness.Window);
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(201);
        OverlayLabels(harness.Window).Should().BeEmpty();
        harness.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WhileLoading_HostsOverlayOnContentContainerNotLoadingView()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = CreateGatedDrillDown(matchedInFirstBatch: 50, matchedInSecondBatch: 0, totalBatches: 2);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        await using GatedScanScope scope = new(rows, harness);
        harness.State.EnterFocusedTable(drillDown);

        // Act — the scan is blocked below the reveal threshold, so the loading view stays current
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // Assert
        LoadingView loadingView = harness.ViewManager.GetCurrentView().Should().BeOfType<LoadingView>().Which;
        Label overlayLabel = OverlayLabels(harness.Window).Should().ContainSingle().Subject;
        overlayLabel.SuperView.Should().BeSameAs(loadingView.SuperView);
        SelfAndDescendants(loadingView).Where(IsProgressOverlayLabel).Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WhenContentReplacedAfterMidScanReveal_RemovesOverlayBeforeStaleCallbacksRun()
    {
        // Arrange — the table is revealed mid-scan while the worker is blocked
        using IApplication app = TestAppFactory.Create();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = CreateGatedDrillDown(matchedInFirstBatch: 201, matchedInSecondBatch: 0, totalBatches: 2);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        await using GatedScanScope scope = new(rows, harness);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);
        harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>();
        OverlayLabels(harness.Window).Should().ContainSingle();

        // Act — replace the content while the scan is still running; no queued callback has run yet
        harness.ViewManager.ShowError("boom");

        // Assert
        OverlayLabels(harness.Window).Should().BeEmpty();
        harness.ViewManager.GetCurrentView().Should().BeOfType<PlaceholderView>();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WhenParentTokenCancelledAfterWorkerFinished_DiscardsQueuedCallbacksAndDisposeIsSafe()
    {
        // Arrange — 500 matches: the reveal, tick and completion queue up, none executed, so
        // the session is still current when the parent token is canceled
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 500) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        LoadingView loadingView = harness.ViewManager.GetCurrentView().Should().BeOfType<LoadingView>().Which;
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 3);

        // Act — canceling the parent token cancels the linked session token
        harness.State.Cts.Cancel();
        ExecuteRemainingCallbacks(harness.Captured);

        // Assert — no table reveal, no redraw, no selection reset, no overlay dismissal
        harness.ViewManager.GetCurrentView().Should().BeSameAs(loadingView);
        OverlayLabels(harness.Window).Should().ContainSingle();
        harness.Captured.Should().BeEmpty();

        Action disposeAfterParentCancel = () => harness.ViewManager.Dispose();
        disposeAfterParentCancel.Should().NotThrow();
    }

    [Theory]
    [InlineData(50, 1)]   // faults before the reveal threshold fired: the failure alone is queued
    [InlineData(250, 2)]  // faults after the threshold revealed the table: reveal + failure
    public async Task SwitchToFocusedTable_WhenScanFaults_ShowsErrorAndReleasesSession(
        int matchedRows,
        int expectedCallbacks)
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = CreateFaultingDrillDown(matchedRows) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);

        // Act — the row read faults mid-batch; once the recorded task ends, the queued
        // callbacks are the reveal (when the threshold fired) and the failure, in that order
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        Func<Task> assertScan = () => harness.Runner.TaskAt(0);
        await assertScan.Should().ThrowAsync<InvalidOperationException>();
        await WaitForCallbacksAsync(harness.Captured, expectedCount: expectedCallbacks);
        ExecuteRemainingCallbacks(harness.Captured);

        // Assert — the failure retires the session and replaces the scan's views with the
        // error placeholder; no overlay survives, nothing stays queued, disposal stays safe
        harness.State.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        PlaceholderView placeholder = harness.ViewManager.GetCurrentView().Should().BeOfType<PlaceholderView>().Which;
        placeholder.Text.Should().Contain("row read failed");
        OverlayLabels(harness.Window).Should().BeEmpty();
        harness.Captured.Should().BeEmpty();
        Action disposeAfterFailure = () => harness.ViewManager.Dispose();
        disposeAfterFailure.Should().NotThrow();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WhenScanFaultsAfterThreshold_DisposesRevealedTransformer()
    {
        // Arrange — 250 matches reveal the table before the row read faults
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = CreateFaultingDrillDown(250) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);

        // Act — the reveal is enqueued before the fault's callback, so executing it first
        // stages the table; the failure callback then retires the session
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        Func<Task> assertScan = () => harness.Runner.TaskAt(0);
        await assertScan.Should().ThrowAsync<InvalidOperationException>();
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        Action onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        FocusedTableTransformer transformer = GetFocusedTableTransformer(
            harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which);
        ExecuteRemainingCallbacks(harness.Captured);

        // Assert — the error placeholder is up and the session released the transformer
        harness.ViewManager.GetCurrentView().Should().BeOfType<PlaceholderView>();
        Func<object> readDisposedTransformer = () => transformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
        harness.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithStaleFailureCallbackAfterReturn_KeepsRestoredTree()
    {
        // Arrange — JsonArrayTree backing data so the drill-down can return to the tree
        string filePath = _tempFiles.Create(".json", "[1,2,3]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = CreateFaultingDrillDown(matchedRows: 50) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.StartNewFile(filePath);
        harness.State.BeginIndexedTreeLoad(indexer);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 1); // failure queued

        // Act — returning to the tree retires the session before the failure callback runs
        harness.ViewManager.ReturnFromDrillDown();
        ExecuteRemainingCallbacks(harness.Captured);

        // Assert — the stale failure callback cannot replace the restored tree
        harness.State.CurrentMode.Should().Be(ViewMode.JsonArrayTree);
        harness.ViewManager.GetCurrentView().Should().BeOfType<JsonArrayTreeView>();
        harness.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithGateRightAfterThresholdMatch_RevealsTableBeforeFirstTick()
    {
        // Arrange — the gate sits on the row right after the 200th match, so the threshold
        // notification queues while no 1,000-row batch tick has fired yet
        using IApplication app = TestAppFactory.Create();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = CreateGatedDrillDown(
            matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 2, gateRowIndex: 200);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        await using GatedScanScope scope = new(rows, harness);
        harness.State.EnterFocusedTable(drillDown);

        // Act — while the worker is provably blocked, the threshold reveal is the only callback
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 1);
        ExecuteRemainingCallbacks(harness.Captured);

        // Assert — the table is revealed mid-scan, before any progress tick exists
        FocusedTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(200);
        Label overlayLabel = OverlayLabels(harness.Window).Should().ContainSingle().Subject;
        overlayLabel.SuperView.Should().BeSameAs(view.SuperView, "the overlay rides the content container, not the scrolling table");
        SelfAndDescendants(view).Where(IsProgressOverlayLabel).Should().BeEmpty();
        List<Terminal.Gui.ViewBase.View> drawOrder = [.. (view.SuperView?.SubViews ?? [])];
        drawOrder.IndexOf(overlayLabel).Should().BeGreaterThan(drawOrder.IndexOf(view), "the table must not draw over the overlay");
        AssertAtFirstPageAndRow(view);

        // teardown — release the scan and let its remaining notifications drain
        rows.ReleaseGate();
        await WaitForCompletedFocusedTableViewAsync(harness.ViewManager, harness.Captured, harness.Window);
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(200);
        OverlayLabels(harness.Window).Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshCurrentTableView_WithClearedFilterAndQueuedScanCallbacks_IgnoresStaleCallbacks()
    {
        // Arrange — one match: its tick and completion queue up while the loading view stays
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Alice3").Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);

        // Act — clearing the filter rebuilds the view directly, without a new scan
        harness.State.ClearDrillDownActions();
        harness.ViewManager.RefreshCurrentTableView();

        // Assert — the stale tick and completion leave the new unfiltered view untouched
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        FocusedTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(12);
    }

    [Fact]
    public async Task RefreshCurrentTableView_WithAppendedActionAndQueuedScanCallbacks_IgnoresStaleCallbacks()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Alice3").Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);

        // Act — appending a transform action rebuilds the transformer and starts a new scan
        harness.State.AddDrillDownAction(new RenameColumnAction { OldName = "name", NewName = "label" });
        harness.ViewManager.RefreshCurrentTableView();
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 4); // 2 stale + 2 from the new scan

        // Assert — the stale pair plus the new scan's pair end at the new transformer's view
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        FocusedTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(1);
        view.Table.ColumnNames.Should().Contain("label (text)");
    }

    [Fact]
    public async Task ReturnFromDrillDown_WithQueuedScanCallbacks_IgnoresStaleCallbacks()
    {
        // Arrange — JsonArrayTree backing data so the drill-down can return to the tree
        string filePath = _tempFiles.Create(".json", "[1,2,3]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Alice3").Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.StartNewFile(filePath);
        harness.State.BeginIndexedTreeLoad(indexer);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);

        // Act
        harness.ViewManager.ReturnFromDrillDown();

        // Assert — the tree stays restored after the stale scan callbacks run
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        harness.ViewManager.GetCurrentView().Should().BeOfType<JsonArrayTreeView>();
    }

    [Fact]
    public async Task Dispose_WithQueuedScanCallbacks_IgnoresStaleCallbacksAndRepeatedDisposeIsSafe()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Alice3").Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);

        // Act — disposing mid-scan also disposes the session; the second call must be safe
        harness.ViewManager.Dispose();
        harness.ViewManager.Dispose();

        // Assert — the queued stale callbacks stay inert after disposal
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleMorphAction_WithFilterReplacementAfterScanFinished_IgnoresQueuedCompletionAndDisposesOldTransformer()
    {
        // Arrange — 500 matches: the first tick reveals the table, the completion stays queued
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 500) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2); // threshold reveal + first tick
        Action onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        Action onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        FocusedTableView stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        FocusedTableTransformer oldTransformer = GetFocusedTableTransformer(stagedView);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 1); // stale completion queued
        FilterAction replacement = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Alice3").Value;

        // Act — replacing the filter cancels the old scan and starts a new one
        stagedView.OnMorphAction?.Invoke(replacement);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 3); // stale completion + 2 from the new scan

        // Assert — the old session is torn down and its stale completion is inert
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        Func<object> readDisposedTransformer = () => oldTransformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();

        FocusedTableView newView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        newView.Should().NotBeSameAs(stagedView);
        newView.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(1);
    }

    [Fact]
    public async Task HandleMorphAction_WithTransformActionAfterScanFinished_IgnoresQueuedCompletionAndDisposesOldTransformer()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 500) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2); // threshold reveal + first tick
        Action onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        Action onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        FocusedTableView stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        FocusedTableTransformer oldTransformer = GetFocusedTableTransformer(stagedView);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 1);

        // Act — a transform action rebuilds the transformer and restarts the scan; the new
        // scan matches 500 rows again, so it queues its own reveal and tick before completing
        stagedView.OnMorphAction?.Invoke(new RenameColumnAction { OldName = "name", NewName = "label" });
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 4); // stale completion + 3 from the new scan

        // Assert
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        Func<object> readDisposedTransformer = () => oldTransformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();

        FocusedTableView newView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        newView.Should().NotBeSameAs(stagedView);
        newView.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(500);
        newView.Table.ColumnNames.Should().Contain("label (text)");
    }

    [Fact]
    public async Task ReturnFromDrillDown_AfterScanFinishedWithStagedTable_IgnoresQueuedCompletionAndDisposesOldTransformer()
    {
        // Arrange
        string filePath = _tempFiles.Create(".json", "[1,2,3]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 500) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.StartNewFile(filePath);
        harness.State.BeginIndexedTreeLoad(indexer);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2); // threshold reveal + first tick
        Action onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        Action onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        FocusedTableView stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        FocusedTableTransformer oldTransformer = GetFocusedTableTransformer(stagedView);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 1);

        // Act
        harness.ViewManager.ReturnFromDrillDown();

        // Assert
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        harness.ViewManager.GetCurrentView().Should().BeOfType<JsonArrayTreeView>();
        Func<object> readDisposedTransformer = () => oldTransformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_AfterScanFinishedWithStagedTable_IgnoresQueuedCompletionAndIsIdempotent()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        DrillDownState drillDown = CreateNumberedDrillDown(rowCount: 500) with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2); // threshold reveal + first tick
        Action onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        Action onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        FocusedTableView stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        FocusedTableTransformer transformer = GetFocusedTableTransformer(stagedView);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 1);

        // Act
        harness.ViewManager.Dispose();
        harness.ViewManager.Dispose();

        // Assert — the session-owned transformer is disposed and stale callbacks stay inert
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        Func<object> readDisposedTransformer = () => transformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task RefreshCurrentTableView_WithClearedFilterWhileWorkerBlocked_StopsWorkerAfterInFlightBatch()
    {
        // Arrange — three batches; the batch 1 notifications stay queued (loading view) and
        // the worker is blocked on the first row of batch 2
        using IApplication app = TestAppFactory.Create();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        await using GatedScanScope scope = new(rows, harness);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; waiting for both keeps the later drain deterministic
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);

        // Act — clearing the filter cancels the blocked scan; no new scan starts
        harness.State.ClearDrillDownActions();
        harness.ViewManager.RefreshCurrentTableView();
        rows.ReleaseGate();

        // Assert — the recorded scan task ends by cancellation rather than completion, which
        // deterministically proves the worker stopped after batches 1–2 (never reading batch 3)
        Func<Task> assertOldScan = () => harness.Runner.TaskAt(0);
        await assertOldScan.Should().ThrowAsync<OperationCanceledException>();
        rows.ReadCount.Should().Be(2000);

        // the stale notifications leave the new unfiltered view untouched
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        FocusedTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(3000);
    }

    [Fact]
    public async Task ReturnFromDrillDown_WhileWorkerBlocked_StopsWorkerAfterInFlightBatch()
    {
        // Arrange — JsonArrayTree backing data so the drill-down can return to the tree
        string filePath = _tempFiles.Create(".json", "[1,2,3]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        await using GatedScanScope scope = new(rows, harness);
        harness.State.StartNewFile(filePath);
        harness.State.BeginIndexedTreeLoad(indexer);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; waiting for both keeps the later drain deterministic
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);

        // Act — returning to the tree cancels the blocked scan
        harness.ViewManager.ReturnFromDrillDown();
        rows.ReleaseGate();

        // Assert — the recorded scan task ends by cancellation after batches 1–2 only
        Func<Task> assertOldScan = () => harness.Runner.TaskAt(0);
        await assertOldScan.Should().ThrowAsync<OperationCanceledException>();
        rows.ReadCount.Should().Be(2000);

        // the tree stays restored after the stale notifications run
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        harness.ViewManager.GetCurrentView().Should().BeOfType<JsonArrayTreeView>();
    }

    [Fact]
    public async Task Dispose_WhileWorkerBlocked_StopsWorkerAfterInFlightBatchAndIsIdempotent()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        await using GatedScanScope scope = new(rows, harness);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; waiting for both keeps the later drain deterministic
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);

        // Act — disposing mid-scan cancels the blocked worker; the second call must be safe
        harness.ViewManager.Dispose();
        harness.ViewManager.Dispose();
        rows.ReleaseGate();

        // Assert — the recorded scan task ends by cancellation after batches 1–2 only, and
        // the queued notifications stay inert
        Func<Task> assertOldScan = () => harness.Runner.TaskAt(0);
        await assertOldScan.Should().ThrowAsync<OperationCanceledException>();
        rows.ReadCount.Should().Be(2000);
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleMorphAction_WithFilterReplacementWhileWorkerBlocked_StopsOldWorkerAndDisposesOldTransformer()
    {
        // Arrange — three batches; the executed batch 1 notification staged the table with
        // exactly the threshold rows, and the worker is blocked on the first row of batch 2
        using IApplication app = TestAppFactory.Create();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        await using GatedScanScope scope = new(rows, harness);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; execute them in whichever order the threads landed
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);
        FocusedTableView stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        stagedView.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(200);
        FocusedTableTransformer oldTransformer = GetFocusedTableTransformer(stagedView);
        FilterAction replacement = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Match0").Value;

        // Act — replacing the filter cancels the blocked old worker and starts a new scan;
        // the first recorded task is the old worker's, and it must end by cancellation
        stagedView.OnMorphAction?.Invoke(replacement);
        rows.ReleaseGate();
        Func<Task> assertOldScan = () => harness.Runner.TaskAt(0);
        await assertOldScan.Should().ThrowAsync<OperationCanceledException>();
        FocusedTableView newView = await WaitForCompletedFocusedTableViewAsync(harness.ViewManager, harness.Captured, harness.Window);

        // Assert — the old session is torn down and the replacement scan produced the new view
        harness.Captured.Should().BeEmpty();
        Func<object> readDisposedTransformer = () => oldTransformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
        newView.Should().NotBeSameAs(stagedView);
        newView.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(1);
    }

    [Fact]
    public async Task HandleMorphAction_WithTransformActionWhileWorkerBlocked_StopsOldWorkerAndDisposesOldTransformer()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        await using GatedScanScope scope = new(rows, harness);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; execute them in whichever order the threads landed
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);
        FocusedTableView stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        FocusedTableTransformer oldTransformer = GetFocusedTableTransformer(stagedView);

        // Act — a transform action cancels the blocked old worker and restarts the scan; the
        // first recorded task is the old worker's, and it must end by cancellation
        stagedView.OnMorphAction?.Invoke(new RenameColumnAction { OldName = "name", NewName = "label" });
        rows.ReleaseGate();
        Func<Task> assertOldScan = () => harness.Runner.TaskAt(0);
        await assertOldScan.Should().ThrowAsync<OperationCanceledException>();
        FocusedTableView newView = await WaitForCompletedFocusedTableViewAsync(harness.ViewManager, harness.Captured, harness.Window);

        // Assert — no queue-drain assertion here: the new scan also crosses the threshold, so
        // its reveal can legally be queued after the completion the helper waited for
        Func<object> readDisposedTransformer = () => oldTransformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
        newView.Should().NotBeSameAs(stagedView);
        newView.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(200);
        newView.Table.ColumnNames.Should().Contain("label (text)");
    }

    [Fact]
    public async Task ReturnFromDrillDown_WhileWorkerBlockedOnStagedTable_StopsWorkerAndDisposesOldTransformer()
    {
        // Arrange
        string filePath = _tempFiles.Create(".json", "[1,2,3]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        await using GatedScanScope scope = new(rows, harness);
        harness.State.StartNewFile(filePath);
        harness.State.BeginIndexedTreeLoad(indexer);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; execute them in whichever order the threads landed
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);
        FocusedTableView stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        FocusedTableTransformer oldTransformer = GetFocusedTableTransformer(stagedView);

        // Act — returning to the tree cancels the blocked scan; the recorded task must end
        // by cancellation after batches 1–2 only
        harness.ViewManager.ReturnFromDrillDown();
        rows.ReleaseGate();
        Func<Task> assertOldScan = () => harness.Runner.TaskAt(0);
        await assertOldScan.Should().ThrowAsync<OperationCanceledException>();
        rows.ReadCount.Should().Be(2000);

        // Assert — the worker never read batch 3 and the old transformer is disposed
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        harness.ViewManager.GetCurrentView().Should().BeOfType<JsonArrayTreeView>();
        Func<object> readDisposedTransformer = () => oldTransformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_WhileWorkerBlockedOnStagedTable_StopsWorkerDisposesSessionAndIsIdempotent()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        (DrillDownState DrillDown, GatedDrillDownRows Rows) gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using GatedDrillDownRows rows = gated.Rows;
        FilterAction filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        DrillDownState drillDown = gated.DrillDown with { ActionStack = [filter] };
        using FilterScanHarness harness = new();
        await using GatedScanScope scope = new(rows, harness);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; execute them in whichever order the threads landed
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);
        FocusedTableView stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        FocusedTableTransformer transformer = GetFocusedTableTransformer(stagedView);

        // Act — disposing mid-scan cancels the blocked worker; the second call must be safe;
        // the recorded task must end by cancellation after batches 1–2 only
        harness.ViewManager.Dispose();
        harness.ViewManager.Dispose();
        rows.ReleaseGate();
        Func<Task> assertOldScan = () => harness.Runner.TaskAt(0);
        await assertOldScan.Should().ThrowAsync<OperationCanceledException>();
        rows.ReadCount.Should().Be(2000);

        // Assert — the worker never read batch 3 and the session-owned transformer is disposed
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        Func<object> readDisposedTransformer = () => transformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void HandleMorphAction_WithFocusedTableModeAndDrillDown_AppendsToDrillDownActionStack()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        DrillDownState drillDown = new(
            [new FocusedTableRow(Encoding.UTF8.GetBytes("{\"name\":\"Alice\"}"), "[0]")],
            schema,
            ViewMode.JsonArrayTree,
            KeyPath: [],
            ActionStack: []);
        using AppState state = new();
        state.EnterFocusedTable(drillDown);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.SwitchToFocusedTable(drillDown);
        FocusedTableView view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        RenameColumnAction action = new() { OldName = "name", NewName = "label" };

        // Act
        view.OnMorphAction?.Invoke(action);

        // Assert
        state.ActionStack.Should().BeEmpty();
        DrillDownState drillDownAfter = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
        drillDownAfter.ActionStack.Should().Equal(action);
    }

    [Fact]
    public void HandleMorphAction_WithFocusedTableModeAndDrillDown_MarksDrillDownSessionUnsaved()
    {
        // Arrange — the DrillDown replays a just-loaded recipe (clean); applying an action within
        // it must flip its own flag without touching the root one
        using IApplication app = TestAppFactory.Create();
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        DrillDownState drillDown = new(
            [new FocusedTableRow(Encoding.UTF8.GetBytes("{\"name\":\"Alice\"}"), "[0]")],
            schema,
            ViewMode.JsonArrayTree,
            KeyPath: [],
            ActionStack: [new RenameColumnAction { OldName = "name", NewName = "loaded" }]);
        using AppState state = new();
        state.EnterFocusedTable(drillDown);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.SwitchToFocusedTable(drillDown);
        FocusedTableView view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        RenameColumnAction action = new() { OldName = "name", NewName = "label" };

        // Act
        view.OnMorphAction?.Invoke(action);

        // Assert
        DrillDownState drillDownAfter = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
        drillDownAfter.HasUnsavedChanges.Should().BeTrue();
        state.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void HandleMorphAction_WithFocusedTableModeAndBaseActions_LeavesBaseStackUntouched()
    {
        // Arrange — a populated base ActionStack must survive appending a morph action to the
        // active DrillDown's own stack
        using IApplication app = TestAppFactory.Create();
        RenameColumnAction baseAction = new() { OldName = "base", NewName = "renamed_base" };
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        DrillDownState drillDown = new(
            [new FocusedTableRow(Encoding.UTF8.GetBytes("{\"name\":\"Alice\"}"), "[0]")],
            schema,
            ViewMode.JsonArrayTree,
            KeyPath: [],
            ActionStack: []);
        using AppState state = new();
        state.EnterFocusedTable(drillDown);
        state.AddMorphAction(baseAction);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.SwitchToFocusedTable(drillDown);
        FocusedTableView view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        DeleteColumnAction drillDownAction = new() { ColumnName = "drill_down" };

        // Act
        view.OnMorphAction?.Invoke(drillDownAction);

        // Assert
        state.ActionStack.Should().Equal(baseAction);
        DrillDownState drillDownAfter = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
        drillDownAfter.ActionStack.Should().Equal(drillDownAction);
    }

    [Fact]
    public void HandleMorphAction_AfterErrorViewReplacedFocusedTable_AppendsToBaseActionStack()
    {
        // Arrange — an error view replaced FocusedTable, then the base table was shown again;
        // the action must go to the base stack and no DrillDown session may remain
        string filePath = _tempFiles.Create(".csv", "col1,col2\nvalue1,value2\n");
        using IApplication app = TestAppFactory.Create();
        RenameColumnAction drillDownAction = new() { OldName = "x", NewName = "y" };
        DrillDownState drillDown = new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] },
            ViewMode.JsonLinesTree,
            KeyPath: [],
            ActionStack: [drillDownAction]);
        using AppState state = new();
        state.StartNewFile(filePath);
        state.EnterFocusedTable(drillDown);
        state.EnterPlaceholderMode();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.Csv,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }],
        };
        viewManager.SwitchToCsvTable(new CompletedStubRowIndexer(filePath), schema);
        CsvTableView view = viewManager.GetCurrentView().Should().BeOfType<CsvTableView>().Which;
        RenameColumnAction action = new() { OldName = "col1", NewName = "renamed" };

        // Act
        view.OnMorphAction?.Invoke(action);

        // Assert
        state.ActionStack.Should().Equal(action);
        state.TryGetDrillDown(out _).Should().BeFalse();
    }

    [Fact]
    public void DrillDown_OnSuccess_LeavesBaseActionStackUntouched()
    {
        // Arrange — the base table's ActionStack must survive entering a DrillDown, and the fresh
        // DrillDownState must start with its own empty ActionStack
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        SingleDrillDownRequest request = new(
            DataFormat.JsonObject,
            Encoding.UTF8.GetBytes("[{\"a\":1}]"),
            [new KeyPathSegment("list", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        viewManager.DrillDown(request);

        // Assert
        state.ActionStack.Should().ContainSingle();
        DrillDownState drillDown = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
        drillDown.ActionStack.Should().BeEmpty();
    }

    [Fact]
    public void DrillDown_OnFailure_LeavesActionStackUntouched()
    {
        // Arrange — an empty array node fails schema extraction, routing to ShowError
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        SingleDrillDownRequest request = new(
            DataFormat.JsonObject,
            Encoding.UTF8.GetBytes("[]"),
            [new KeyPathSegment("list", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        viewManager.DrillDown(request);

        // Assert — the failed DrillDown leaves the previous actions (and error view) in place
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        state.ActionStack.Should().ContainSingle();
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_OnSuccess_LeavesBaseActionStackUntouched()
    {
        // Arrange — the base table's ActionStack must survive entering a DrillDown, and the fresh
        // DrillDownState must start with its own empty ActionStack
        string filePath = _tempFiles.Create(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        state.AddMorphAction(new RenameColumnAction { OldName = "name", NewName = "label" });
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        FullAggregationDrillDownRequest request = new(
            DataFormat.JsonLines,
            [new KeyPathSegment("user", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        await viewManager.FullAggregationDrillDownAsync(request);

        // Assert
        state.ActionStack.Should().ContainSingle();
        DrillDownState drillDown = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
        drillDown.ActionStack.Should().BeEmpty();
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_OnFailure_LeavesActionStackUntouched()
    {
        // Arrange — a non-matching KeyPath forces the scan to fail
        string filePath = _tempFiles.Create(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        state.AddMorphAction(new RenameColumnAction { OldName = "name", NewName = "label" });
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        FullAggregationDrillDownRequest request = new(
            DataFormat.JsonLines,
            [new KeyPathSegment("missing", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        await viewManager.FullAggregationDrillDownAsync(request);

        // Assert — the failed scan leaves the previous actions (and error view) in place
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        state.ActionStack.Should().ContainSingle();
    }

    [Fact]
    public void ReturnFromDrillDown_WithJsonLinesTreePreviousMode_RestoresJsonLinesTree()
    {
        // Arrange
        string filePath = _tempFiles.Create(".jsonl", "{\"col1\": \"value\"}\n");
        using IApplication app = TestAppFactory.Create();
        Refedle.Engine.IO.JsonLines.RowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        TableSchema schema = new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using AppState state = new();
        state.StartNewFile(filePath);
        state.BeginIndexedTreeLoad(indexer);
        DrillDownState drillDown = new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonLinesTree, KeyPath: [], ActionStack: []);
        state.EnterFocusedTable(drillDown);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.ReturnFromDrillDown();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
        state.GetDrillDownOrNull().Should().BeNull();
        viewManager.GetCurrentView().Should().BeOfType<JsonLinesTreeView>();
    }

    [Fact]
    public void ReturnFromDrillDown_WithJsonArrayTreePreviousMode_RestoresJsonArrayTree()
    {
        // Arrange
        string filePath = _tempFiles.Create(".json", "[1,2,3]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        TableSchema schema = new() { SourceFormat = DataFormat.JsonArray, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using AppState state = new();
        state.StartNewFile(filePath);
        state.BeginIndexedTreeLoad(indexer);
        DrillDownState drillDown = new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonArrayTree, KeyPath: [], ActionStack: []);
        state.EnterFocusedTable(drillDown);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.ReturnFromDrillDown();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonArrayTree);
        state.GetDrillDownOrNull().Should().BeNull();
        viewManager.GetCurrentView().Should().BeOfType<JsonArrayTreeView>();
    }

    [Fact]
    public void ReturnFromDrillDown_WithJsonObjectTreePreviousMode_RestoresJsonObjectTree()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        IReadOnlyList<JsonObjectEntry> entries = [new JsonObjectEntry("id", Encoding.UTF8.GetBytes("1"))];
        TableSchema schema = new() { SourceFormat = DataFormat.JsonObject, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using AppState state = new();
        state.StartNewFile("test.json");
        state.EnterJsonObjectTree(entries);
        DrillDownState drillDown = new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonObjectTree, KeyPath: [], ActionStack: []);
        state.EnterFocusedTable(drillDown);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.ReturnFromDrillDown();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonObjectTree);
        state.GetDrillDownOrNull().Should().BeNull();
        viewManager.GetCurrentView().Should().BeOfType<JsonObjectTreeView>();
    }

    [Fact]
    public void ReturnFromDrillDown_WithNullDrillDown_DoesNothing()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.SwitchToFileSelection();

        // Act
        viewManager.ReturnFromDrillDown();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.FileSelection);
        state.GetDrillDownOrNull().Should().BeNull();
    }

    [Fact]
    public void ReturnFromDrillDown_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.Dispose();

        // Act
        Action act = () => viewManager.ReturnFromDrillDown();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    /// <summary>
    /// A ViewManager showing a two-column CSV table whose UI-thread dispatcher queues callbacks
    /// instead of running them, so a test decides when a queued callback runs.
    /// </summary>
    private sealed class RaggedCsvFixture : IDisposable
    {
        private readonly Terminal.Gui.App.IApplication _app;
        private readonly Window _window;

        private RaggedCsvFixture(
            Terminal.Gui.App.IApplication app,
            AppState state,
            Window window,
            ViewManager viewManager,
            List<Action> queued,
            Refedle.Engine.IO.Csv.DataRowIndexer indexer,
            TableSchema schema)
        {
            _app = app;
            State = state;
            _window = window;
            ViewManager = viewManager;
            Queued = queued;
            Indexer = indexer;
            Schema = schema;
        }

        public AppState State { get; }
        public ViewManager ViewManager { get; }
        public List<Action> Queued { get; }
        public Refedle.Engine.IO.Csv.DataRowIndexer Indexer { get; }
        public TableSchema Schema { get; }

        public static RaggedCsvFixture Create(string filePath)
        {
            IApplication app = TestAppFactory.Create();
            AppState state = new();
            state.StartNewFile(filePath);
            Window window = new();
            List<Action> queued = [];
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), queued.Add);
            DataRowIndexer indexer = new(filePath);
            indexer.BuildIndex();
            TableSchema schema = new()
            {
                SourceFormat = DataFormat.Csv,
                Columns =
                [
                    new ColumnSchema { Name = "col1", Type = ColumnType.Text },
                    new ColumnSchema { Name = "col2", Type = ColumnType.Text }
                ]
            };
            state.CompleteCsvLoad(indexer, schema);
            viewManager.SwitchToCsvTable(indexer, schema);
            return new RaggedCsvFixture(app, state, window, viewManager, queued, indexer, schema);
        }

        public void Dispose()
        {
            ViewManager.Dispose();
            _window.Dispose();
            State.Dispose();
            _app.Dispose();
        }
    }

    /// <summary>
    /// Loads a single-row JSON Lines table with the given actions and exposes the schema
    /// refinement callback registered by <see cref="ViewManager"/>.
    /// </summary>
    private sealed class SchemaRefinementHarness : IDisposable
    {
        private readonly TempFile _tempFile = TempFile.ReservePath(".jsonl");
        private readonly string _filePath;
        private readonly Terminal.Gui.App.IApplication _app = TestAppFactory.Create();
        private readonly ViewManager _viewManager;

        public SchemaRefinementHarness(IReadOnlyList<IMorphAction> actions, params string[] initialColumns)
            : this(actions, 2, initialColumns)
        {
        }

        public SchemaRefinementHarness(IReadOnlyList<IMorphAction> actions, long totalRows, params string[] initialColumns)
        {
            _filePath = _tempFile.Path;
            File.WriteAllText(_filePath, "{\"col1\": \"a\", \"col2\": \"b\"}\n{\"col1\": \"x\", \"col2\": \"y\"}\n");
            State.StartNewFile(_filePath);
            State.SetActionStack(actions);
            ModeController modeController = new(State, action => action(), TestSchemaScannerFactories.JsonLines);
            _viewManager = new ViewManager(Window, State, modeController, Runner, action => action());
            _viewManager.SwitchToJsonLinesTableView(new CompletedStubRowIndexer(_filePath, totalRows), CreateJsonLinesSchema(initialColumns));
            View = _viewManager.GetCurrentView().Should().BeOfType<JsonLinesTableView>().Subject;
        }

        public AppState State { get; } = new();
        public Window Window { get; } = new();
        public PausableFilterScanRunner Runner { get; } = new();
        public ViewManager ViewManager => _viewManager;
        public JsonLinesTableView View { get; }
        public ITableSource Table => View.Table.Should().BeAssignableTo<ITableSource>().Subject;
        public TableSelection Selection => View.Value.Should().BeOfType<TableSelection>().Subject;

        public void Refine(params string[] columns)
        {
            State.OnSchemaRefined.Should().NotBeNull();
            State.OnSchemaRefined(CreateJsonLinesSchema(columns));
        }

        public void Dispose()
        {
            _viewManager.Dispose();
            Window.Dispose();
            State.Dispose();
            _app.Dispose();
            _tempFile.Dispose();
        }
    }

    /// <summary>
    /// <see cref="IFilterScanRunner"/> double that records each call and holds the scan
    /// until <see cref="Resume"/>, then builds the index on the calling indexer.
    /// </summary>
    private sealed class PausableFilterScanRunner : IFilterScanRunner
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task _completion = Task.CompletedTask;

        public List<CapturingFilterScanRunner.Call> Calls { get; } = [];

        public Task Completion => _completion;

        public void Resume() => _gate.TrySetResult();

        public Task RunAsync(IFilterRowIndexer filterIndexer, CancellationToken ct)
        {
            Calls.Add(new CapturingFilterScanRunner.Call(filterIndexer, ct));
            _completion = RunAfterGateAsync(filterIndexer, ct);
            return _completion;
        }

        private async Task RunAfterGateAsync(IFilterRowIndexer filterIndexer, CancellationToken ct)
        {
            await _gate.Task.ConfigureAwait(false);
            await filterIndexer.BuildIndexAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// <see cref="IFilterScanRunner"/> double that records each call and completes immediately.
    /// </summary>
    private sealed class CapturingFilterScanRunner : IFilterScanRunner
    {
        public sealed record Call(IFilterRowIndexer Indexer, CancellationToken Token);

        public List<Call> Calls { get; } = [];

        public Task RunAsync(IFilterRowIndexer filterIndexer, CancellationToken ct)
        {
            Calls.Add(new Call(filterIndexer, ct));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// ViewManager wired to a captured UI-invoke queue, so worker-fired filter-scan callbacks
    /// stay queued until the test executes them on the UI thread deterministically.
    /// </summary>
    private sealed class FilterScanHarness : IDisposable
    {
        public AppState State { get; } = new();
        public Window Window { get; } = new();
        public System.Collections.Concurrent.ConcurrentQueue<Action> Captured { get; } = [];
        public TaskRecordingFilterScanRunner Runner { get; } = new();
        public ViewManager ViewManager { get; }

        public FilterScanHarness()
        {
            ModeController modeController = new(State, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager = new ViewManager(Window, State, modeController, Runner, Captured.Enqueue);
        }

        public void Dispose()
        {
            ViewManager.Dispose();
            Window.Dispose();
            State.Dispose();
        }
    }

    /// <summary>
    /// Async-disposable scope for gated filter-scan tests: on every exit — setup failure,
    /// timeout, or failed assertion — it stops the scan (ViewManager disposal cancels the
    /// session), releases the gate, and joins every started worker with a bounded wait,
    /// before the gate event and the shared harness state are disposed by their usings.
    /// </summary>
    private sealed class GatedScanScope(GatedDrillDownRows rows, FilterScanHarness harness) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            harness.ViewManager.Dispose();
            rows.ReleaseGate();
            foreach (Task task in harness.Runner.Tasks())
            {
                // WhenAny observes a canceled or faulted worker without throwing; a worker
                // stuck past the bound fails the test instead of hanging cleanup
                await Task.WhenAny(task.WaitAsync(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
                task.IsCompleted.Should().BeTrue("a scan worker must terminate within the bounded cleanup wait");
            }
        }
    }

    /// <summary>
    /// Row list whose read of the configured row throws, so ViewManager-level tests can
    /// fault the background filter scan at a chosen row without any timing dependency.
    /// </summary>
    private sealed class FaultingDrillDownRows(FocusedTableRow[] rows, int faultRowIndex)
        : IReadOnlyList<FocusedTableRow>
    {
        public int Count => rows.Length;

        public FocusedTableRow this[int index] =>
            index == faultRowIndex ? throw new InvalidOperationException("row read failed") : rows[index];

        public IEnumerator<FocusedTableRow> GetEnumerator() => ((IReadOnlyList<FocusedTableRow>)rows).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            rows.GetEnumerator();
    }

    private static TableSchema CreateItemCountSchema(DataFormat format) => new()
    {
        SourceFormat = format,
        Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }],
    };

    private static FilterAction CreateColumnFilter(string value) =>
        FilterAction.Create("col1", FilterOperator.Equals, ComparisonType.Text, value).Value;

    private static DataRowIndexer BuildCsvIndexer(string filePath)
    {
        DataRowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        return indexer;
    }

    private static TableSchema CreateJsonLinesSchema(params string[] columnNames) =>
        new()
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [.. columnNames.Select((name, index) => new ColumnSchema { Name = name, Type = ColumnType.Text, ColumnIndex = index })],
        };

    private static DrillDownState CreateNumberedDrillDown(int rowCount)
    {
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        FocusedTableRow[] rows = [.. Enumerable.Range(0, rowCount).Select(index => new FocusedTableRow(Encoding.UTF8.GetBytes($"{{\"name\":\"Alice{index}\"}}"), $"[{index}]"))];
        return new DrillDownState(rows, schema, ViewMode.JsonArrayTree, KeyPath: [], ActionStack: []);
    }

    /// <summary>
    /// DrillDown with 1250 rows (two indexer batches of 1000 + 250) whose "name" values
    /// contain "Match" exactly <paramref name="matchedInFirstBatch"/> /
    /// <paramref name="matchedInSecondBatch"/> times per batch, for threshold-boundary tests.
    /// </summary>
    private static DrillDownState CreateMixedDrillDown(int matchedInFirstBatch, int matchedInSecondBatch)
    {
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        IEnumerable<string> names = Enumerable.Range(0, matchedInFirstBatch).Select(static i => $"Match{i}")
            .Concat(Enumerable.Range(0, 1000 - matchedInFirstBatch).Select(static i => $"Other{i}"))
            .Concat(Enumerable.Range(0, matchedInSecondBatch).Select(static i => $"Match{1000 + i}"))
            .Concat(Enumerable.Range(0, 250 - matchedInSecondBatch).Select(static i => $"Other{1000 + i}"));
        FocusedTableRow[] rows = [.. names.Select(static (name, index) => new FocusedTableRow(Encoding.UTF8.GetBytes($"{{\"name\":\"{name}\"}}"), $"[{index}]"))];
        return new DrillDownState(rows, schema, ViewMode.JsonArrayTree, KeyPath: [], ActionStack: []);
    }

    private static IEnumerable<Terminal.Gui.ViewBase.View> SelfAndDescendants(Terminal.Gui.ViewBase.View view)
    {
        Stack<Terminal.Gui.ViewBase.View> pending = new();
        pending.Push(view);

        while (pending.Count > 0)
        {
            Terminal.Gui.ViewBase.View current = pending.Pop();
            yield return current;

            foreach (Terminal.Gui.ViewBase.View child in current.SubViews)
            {
                pending.Push(child);
            }
        }
    }

    private static string? GetItemCountText(Window window) =>
        window.SubViews
            .OfType<Label>()
            .Select(static label => label.Text)
            .SingleOrDefault(static text => text.EndsWith(" items", StringComparison.Ordinal));

    /// <summary>
    /// Signal-based replacement for polling: each captured UI-thread callback is read from a channel
    /// with a bounded wait, so a callback that never arrives fails the test instead of hanging it.
    /// </summary>
    private sealed class SteppableCallbackQueue
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
    private sealed class OutcomeFilterScanRunner : IFilterScanRunner
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

    /// <summary>
    /// ViewManager whose UI-thread invoke is captured in a <see cref="SteppableCallbackQueue"/>, so scan
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
        public SteppableCallbackQueue Queue { get; } = new();
        public ViewManager ViewManager { get; }

        public void Dispose()
        {
            ViewManager.Dispose();
            Window.Dispose();
            _statusBar.Dispose();
            State.Dispose();
        }
    }

    private enum ScanOutcome
    {
        Faulted,
        Canceled,
    }
}

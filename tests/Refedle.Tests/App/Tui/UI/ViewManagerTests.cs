using System.Text;
using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonArray;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.Tests.App.Tui.Workers.Schema;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

public sealed class ViewManagerTests : IDisposable
{
    private readonly List<string> _tempFiles = [];
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            _disposed = true;
        }
    }

    private string CreateTempFile(string extension, string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        File.WriteAllText(filePath, content);
        _tempFiles.Add(filePath);
        return filePath;
    }

    [Fact]
    public void RefreshStatusBarHints_WithNoFilePath_UsesDefaultHints()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        var currentStatusBar = viewManager.GetCurrentStatusBar();
        currentStatusBar.Should().NotBeNull();
        var hints = Enumerable.Select(
                Enumerable.OfType<Shortcut>(currentStatusBar.SubViews),
                s => s.HelpText);
        hints.Should().BeEquivalentTo(
            ["o:Open", "s:Save", "q:Quit", "?:Help"],
            opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void RefreshStatusBarHints_WithCsvFilePath_UsesDefaultHints()
    {
        // Arrange
        var filePath = CreateTempFile(".csv", "col1,col2\nvalue1,value2\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        var currentStatusBar = viewManager.GetCurrentStatusBar();
        currentStatusBar.Should().NotBeNull();
        var hints = Enumerable.Select(
                Enumerable.OfType<Shortcut>(currentStatusBar.SubViews),
                s => s.HelpText);
        hints.Should().BeEquivalentTo(
            ["o:Open", "s:Save", "q:Quit", "?:Help"],
            opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void RefreshStatusBarHints_WithJsonLinesPath_IncludesToggleHint()
    {
        // Arrange
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"value\"}\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        var currentStatusBar = viewManager.GetCurrentStatusBar();
        currentStatusBar.Should().NotBeNull();
        var hints = Enumerable.Select(
                Enumerable.OfType<Shortcut>(currentStatusBar.SubViews),
                s => s.HelpText);
        hints.Should().Contain("t:Tree/Table");
    }

    [Fact]
    public void RefreshStatusBarHints_WithJsonArrayFilePath_IncludesToggleHint()
    {
        // Arrange
        var filePath = CreateTempFile(".json", "[1,2,3]");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        var currentStatusBar = viewManager.GetCurrentStatusBar();
        currentStatusBar.Should().NotBeNull();
        var hints = Enumerable.Select(
                Enumerable.OfType<Shortcut>(currentStatusBar.SubViews),
                s => s.HelpText);
        hints.Should().Contain("t:Tree/Table");
    }

    [Fact]
    public void RefreshStatusBarHints_WithMorphTableView_IncludesMenuHint()
    {
        // Arrange
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"value\"}\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        var schema = new TableSchema
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };
        viewManager.SwitchToCsvTable(new MockRowIndexer(filePath), schema);

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        var currentStatusBar = viewManager.GetCurrentStatusBar();
        currentStatusBar.Should().NotBeNull();
        var hints = Enumerable.Select(
                Enumerable.OfType<Shortcut>(currentStatusBar.SubViews),
                s => s.HelpText);
        hints.Should().Contain("x:Menu");
    }

    [Fact]
    public void RefreshStatusBarHints_WithFocusedTableMode_IncludesBackHint()
    {
        // Arrange
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"value\"}\n");
        using var app = TestAppFactory.Create();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        var drillDown = new DrillDownState([new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonLinesTree, KeyPath: [], ActionStack: []);
        using var state = new AppState();
        state.StartNewFile(filePath);
        state.EnterFocusedTable(drillDown);
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        var currentStatusBar = viewManager.GetCurrentStatusBar();
        currentStatusBar.Should().NotBeNull();
        currentStatusBar.SubViews.OfType<Shortcut>().Select(s => s.HelpText).Should().Contain("bs:Back");
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenToggleFails_ShowsError()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var indexer = new MockRowIndexer("test.jsonl");
        using var state = new AppState();
        state.BeginIndexedTreeLoad(indexer);
        state.EnterJsonLinesTree();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

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
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"value\"}\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Setup a valid table state
        var schema = new TableSchema
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };
        var indexer = new MockRowIndexer(filePath);
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
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"value\"}\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        state.EnterJsonLinesTree();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Setup a valid tree state
        var schema = new TableSchema
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };
        var indexer = new MockRowIndexer(filePath);
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
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        var task = viewManager.ToggleJsonArrayModeAsync();

        // Assert
        await task;
        task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleJsonArrayModeAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.Dispose();

        // Act
        var act = () => viewManager.ToggleJsonArrayModeAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void SwitchToJsonArrayTree_WithValidIndexer_SetsCurrentView()
    {
        // Arrange
        var filePath = CreateTempFile(".json", "[1,2,3]");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        var indexer = new RowIndexer(filePath);
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
        var filePath = CreateTempFile(".json", "[1,2,3]");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        state.EnterJsonArrayTree();
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        var indexer = new RowIndexer(filePath);

        // Act — do NOT call BuildIndex, so IsIndexingCompleted stays false
        viewManager.SwitchToJsonArrayTree(indexer);

        // Assert — item count is not shown until BuildIndexCompleted fires
        var currentStatusBar = viewManager.GetCurrentStatusBar();
        currentStatusBar.Should().NotBeNull();
        var hints = Enumerable.Select(
                Enumerable.OfType<Shortcut>(currentStatusBar.SubViews),
                s => s.HelpText);
        hints.Should().NotContainMatch("*items*");
        window.SubViews.OfType<Label>().Should().NotContain(l => l.Text.Contains("items", StringComparison.Ordinal));
    }

    [Fact]
    public void RefreshStatusBarHints_WithNoParam_RemovesExistingItemCountLabel()
    {
        // Arrange
        var filePath = CreateTempFile(".csv", "col1\nval1");
        using var app = TestAppFactory.Create();
        var indexer = new MockRowIndexer("test.csv", 5000);
        var schema = new TableSchema { SourceFormat = DataFormat.Csv, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState();
        state.StartNewFile(filePath);
        state.CompleteCsvLoad(indexer, schema);
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.RefreshStatusBarHints();
        window.SubViews.OfType<Label>().First(l => l.Text == "5000 items").Text.Should().Be("5000 items");

        // Act — clear RowIndexer so the label won't be re-added
        state.BeginJsonObjectLoad();
        viewManager.RefreshStatusBarHints();

        // Assert
        window.SubViews.OfType<Label>().Should().NotContain(l => l.Text == "5000 items");
    }

    [Fact]
    public void RefreshStatusBarHints_WithCompletedIndexer_ShowsItemCountLabel()
    {
        // Arrange
        var filePath = CreateTempFile(".csv", "col1\nval1");
        using var app = TestAppFactory.Create();
        var indexer = new MockRowIndexer("test.csv", 5000);
        var schema = new TableSchema { SourceFormat = DataFormat.Csv, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState();
        state.StartNewFile(filePath);
        state.CompleteCsvLoad(indexer, schema);
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        window.SubViews.OfType<Label>().First(l => l.Text == "5000 items").Text.Should().Be("5000 items");
    }

    [Fact]
    public void RefreshStatusBarHints_WithUpdatedIndexer_ReplacesCountOnSubsequentCall()
    {
        // Arrange
        var filePath = CreateTempFile(".csv", "col1\nval1");
        using var app = TestAppFactory.Create();
        var indexer = new MockRowIndexer("test.csv", 1000);
        var schema = new TableSchema { SourceFormat = DataFormat.Csv, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState();
        state.StartNewFile(filePath);
        state.CompleteCsvLoad(indexer, schema);
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act — first call
        viewManager.RefreshStatusBarHints();

        // Assert — shows count from first indexer
        window.SubViews.OfType<Label>().First(l => l.Text == "1000 items").Text.Should().Be("1000 items");

        // Act — replace indexer with one that has a different row count
        var reloadedIndexer = new MockRowIndexer("test.csv", 5000);
        state.CompleteCsvLoad(reloadedIndexer, schema);
        viewManager.RefreshStatusBarHints();

        // Assert — shows updated count, old count is gone
        window.SubViews.OfType<Label>().First(l => l.Text == "5000 items").Text.Should().Be("5000 items");
        window.SubViews.OfType<Label>().Should().NotContain(l => l.Text == "1000 items");
    }

    [Fact]
    public void SwitchToJsonArrayTree_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        IRowIndexer? nullIndexer = null;

        // Act
        var act = () => viewManager.SwitchToJsonArrayTree(nullIndexer!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SwitchToJsonArrayTree_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.Dispose();

        // Act
        var act = () => viewManager.SwitchToJsonArrayTree(new MockRowIndexer("test.json"));

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void SwitchToJsonObjectTree_WithValidEntries_SetsCurrentView()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile("test.json");
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
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
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        IReadOnlyList<JsonObjectEntry>? nullEntries = null;

        // Act
        var act = () => viewManager.SwitchToJsonObjectTree(nullEntries!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SwitchToJsonObjectTree_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.Dispose();
        IReadOnlyList<JsonObjectEntry> entries = [];

        // Act
        var act = () => viewManager.SwitchToJsonObjectTree(entries);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void RefreshStatusBarHints_WithJsonObjectFilePath_DoesNotIncludeToggleHint()
    {
        // Arrange
        var filePath = CreateTempFile(".json", "{\"id\":1}");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        IReadOnlyList<JsonObjectEntry> entries =
        [
            new JsonObjectEntry("id", System.Text.Encoding.UTF8.GetBytes("1")),
        ];
        state.EnterJsonObjectTree(entries);
        viewManager.SwitchToJsonObjectTree(entries);

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        var currentStatusBar = viewManager.GetCurrentStatusBar();
        currentStatusBar.Should().NotBeNull();
        var hints = Enumerable.Select(
            Enumerable.OfType<Shortcut>(currentStatusBar.SubViews),
            s => s.HelpText);
        hints.Should().NotContainMatch("*Tree/Table*");
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WhenScanSucceeds_SwitchesToFocusedTable()
    {
        // Arrange — real file + matching KeyPath; ModeController is sealed and can't be mocked, so
        // success is driven by an actual scan over an actual file.
        var filePath = CreateTempFile(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        var request = new FullAggregationDrillDownRequest(
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
        var filePath = CreateTempFile(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        var request = new FullAggregationDrillDownRequest(
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
        var filePath = CreateTempFile(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);

        List<Action> capturedCallbacks = [];
        using var viewManager = new ViewManager(
            window, state, modeController, new FilterIndexingRunner(), cb => capturedCallbacks.Add(cb));

        var request = new FullAggregationDrillDownRequest(
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
        var filePath = CreateTempFile(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);

        List<Action> capturedCallbacks = [];
        // Not 'using' — disposed manually below to simulate the race between scan completion and dispatch.
        var viewManager = new ViewManager(
            window, state, modeController, new FilterIndexingRunner(), cb => capturedCallbacks.Add(cb));

        var request = new FullAggregationDrillDownRequest(
            DataFormat.JsonLines,
            [new KeyPathSegment("user", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act — scan completes and the callback is captured; dispose BEFORE the callback executes
        await viewManager.FullAggregationDrillDownAsync(request);
        viewManager.Dispose();

        // Assert — the fail-fast guard throws before any state mutation.
        capturedCallbacks.Should().ContainSingle();
        var act = () => capturedCallbacks[0].Invoke();
        act.Should().Throw<ObjectDisposedException>();
        state.GetDrillDownOrNull().Should().BeNull();
        state.CurrentMode.Should().Be(ViewMode.FileSelection);
    }

    [Fact]
    public void SwitchToCsvTable_WithStaleKeyPath_ResetsCurrentKeyPath()
    {
        // Arrange — a leftover KeyPath from a prior Tree/FocusedTable session must not leak into CsvTable
        var filePath = CreateTempFile(".csv", "col1\nvalue1\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        state.SetCurrentKeyPath([new KeyPathSegment("stale", KeyPathSegmentKind.Key)]);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        var schema = new TableSchema
        {
            SourceFormat = DataFormat.Csv,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };

        // Act
        viewManager.SwitchToCsvTable(new MockRowIndexer(filePath), schema);

        // Assert
        state.CurrentKeyPath.Should().BeEmpty();
        window.SubViews.OfType<BreadcrumbBar>().Single().Text.Should().BeEmpty();
    }

    [Fact]
    public void SwitchToJsonLinesTableView_WithStaleKeyPath_ResetsCurrentKeyPath()
    {
        // Arrange — a leftover KeyPath from a prior Tree/FocusedTable session must not leak into JsonLinesTable
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"value\"}\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        state.SetCurrentKeyPath([new KeyPathSegment("stale", KeyPathSegmentKind.Key)]);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        var schema = new TableSchema
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };

        // Act
        viewManager.SwitchToJsonLinesTableView(new MockRowIndexer(filePath), schema);

        // Assert
        state.CurrentKeyPath.Should().BeEmpty();
        window.SubViews.OfType<BreadcrumbBar>().Single().Text.Should().BeEmpty();
    }

    [Fact]
    public void SwitchToFileSelection_WithStaleKeyPath_ResetsCurrentKeyPath()
    {
        // Arrange — a leftover KeyPath from a prior Tree/FocusedTable session must not leak into FileSelection
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.SetCurrentKeyPath([new KeyPathSegment("stale", KeyPathSegmentKind.Key)]);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

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
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        var request = new SingleDrillDownRequest(
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
        var filePath = CreateTempFile(".jsonl", "{\"list\":[{\"a\":1}]}\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        var request = new FullAggregationDrillDownRequest(
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
        using var app = TestAppFactory.Create();
        var schema = new TableSchema
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
        using var state = new AppState();
        state.EnterFocusedTable(drillDown);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.SwitchToFocusedTable(drillDown);

        // Assert — callbacks are wired even with no actions, so Morph works from the first DrillDown
        var view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        var decorated = view.Table.Should().BeOfType<PagedTableSource>().Which
            .Inner.Should().BeOfType<ColumnWidthStabilizingTableSource>().Which;
        decorated.Inner.Should().BeOfType<FocusedTableSource>();
        view.OnMorphAction.Should().NotBeNull();
        decorated.RawColumnNames[1].Should().Be("name");
    }

    [Fact]
    public void RefreshCurrentTableView_WithFocusedTableModeAndDrillDown_RendersThroughFocusedTableTransformer()
    {
        // Arrange — a pending action proves the re-render went through SwitchToFocusedTable's wrap path
        using var app = TestAppFactory.Create();
        var schema = new TableSchema
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
        using var state = new AppState();
        state.EnterFocusedTable(drillDown);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        viewManager.RefreshCurrentTableView();

        // Assert
        var view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which
            .Inner.Should().BeOfType<ColumnWidthStabilizingTableSource>().Which.Inner.Should().BeOfType<FocusedTableTransformer>();
        view.Table.ColumnNames.Should().Contain("label (text)");
    }

    private static DrillDownState CreateNumberedDrillDown(int rowCount)
    {
        var schema = new TableSchema
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        var rows = Enumerable.Range(0, rowCount)
            .Select(index => new FocusedTableRow(Encoding.UTF8.GetBytes($"{{\"name\":\"Alice{index}\"}}"), $"[{index}]"))
            .ToArray();
        return new DrillDownState(rows, schema, ViewMode.JsonArrayTree, KeyPath: [], ActionStack: []);
    }

    private static async Task<Action> WaitForCallbackAsync(System.Collections.Concurrent.ConcurrentQueue<Action> captured)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        Action? callback = null;
        while (!captured.TryDequeue(out callback) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        return callback ?? throw new TimeoutException("The filter scan did not produce a UI callback.");
    }

    private static void ExecuteRemainingCallbacks(System.Collections.Concurrent.ConcurrentQueue<Action> captured)
    {
        while (captured.TryDequeue(out var callback))
        {
            callback();
        }
    }

    private static async Task<FocusedTableView> WaitForFocusedTableViewAsync(
        ViewManager viewManager,
        System.Collections.Concurrent.ConcurrentQueue<Action> captured)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (viewManager.GetCurrentView() is not FocusedTableView && DateTime.UtcNow < deadline)
        {
            var executedAny = false;
            while (captured.TryDequeue(out var callback))
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
        var paged = view.Table.Should().BeOfType<PagedTableSource>().Which;
        paged.CurrentPageNumber.Should().Be(1);
        paged.WindowStart.Should().Be(0);
        view.Value.Should().NotBeNull().And.Subject.As<TableSelection>().SelectedCell.Y.Should().Be(0);
        view.RowOffset.Should().Be(0);
    }

    /// <summary>
    /// DrillDown with 1250 rows (two indexer batches of 1000 + 250) whose "name" values
    /// contain "Match" exactly <paramref name="matchedInFirstBatch"/> /
    /// <paramref name="matchedInSecondBatch"/> times per batch, for threshold-boundary tests.
    /// </summary>
    private static DrillDownState CreateMixedDrillDown(int matchedInFirstBatch, int matchedInSecondBatch)
    {
        var schema = new TableSchema
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        var names = Enumerable.Range(0, matchedInFirstBatch).Select(static i => $"Match{i}")
            .Concat(Enumerable.Range(0, 1000 - matchedInFirstBatch).Select(static i => $"Other{i}"))
            .Concat(Enumerable.Range(0, matchedInSecondBatch).Select(static i => $"Match{1000 + i}"))
            .Concat(Enumerable.Range(0, 250 - matchedInSecondBatch).Select(static i => $"Other{1000 + i}"));
        var rows = names
            .Select(static (name, index) => new FocusedTableRow(Encoding.UTF8.GetBytes($"{{\"name\":\"{name}\"}}"), $"[{index}]"))
            .ToArray();
        return new DrillDownState(rows, schema, ViewMode.JsonArrayTree, KeyPath: [], ActionStack: []);
    }

    /// <summary>
    /// Waits until the captured invoke queue holds <paramref name="expectedCount"/> callbacks
    /// without executing any of them, so their effect stays pending for the test to apply.
    /// </summary>
    private static async Task WaitForCallbacksAsync(
        System.Collections.Concurrent.ConcurrentQueue<Action> captured,
        int expectedCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
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
    /// DrillDown of 1000-row batches whose "name" values contain "Match" the given number of
    /// times per batch ("Other…" otherwise), wrapped in a row list whose read of the row at
    /// <paramref name="gateRowIndex"/> (the first row of the second batch by default) blocks
    /// until released — the worker is provably mid-flight while the gate is closed.
    /// </summary>
    private static (DrillDownState DrillDown, GatedDrillDownRows Rows) CreateGatedDrillDown(
        int matchedInFirstBatch,
        int matchedInSecondBatch,
        int totalBatches,
        int gateRowIndex = 1000)
    {
        var schema = new TableSchema
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        var firstBatch = Enumerable.Range(0, matchedInFirstBatch).Select(static i => $"Match{i}")
            .Concat(Enumerable.Range(0, 1000 - matchedInFirstBatch).Select(static i => $"Other{i}"));
        var secondBatch = Enumerable.Range(0, matchedInSecondBatch).Select(static i => $"Match{1000 + i}")
            .Concat(Enumerable.Range(0, 1000 - matchedInSecondBatch).Select(static i => $"Other{1000 + i}"));
        var laterBatches = Enumerable.Range(2000, (totalBatches - 2) * 1000).Select(static i => $"Other{i}");
        var rows = firstBatch
            .Concat(secondBatch)
            .Concat(laterBatches)
            .Select(static (name, index) => new FocusedTableRow(Encoding.UTF8.GetBytes($"{{\"name\":\"{name}\"}}"), $"[{index}]"))
            .ToArray();
        var gated = new GatedDrillDownRows(rows, gateRowIndex);
        return (new DrillDownState(gated, schema, ViewMode.JsonArrayTree, KeyPath: [], ActionStack: []), gated);
    }

    /// <summary>
    /// DrillDown whose first <paramref name="matchedRows"/> rows match a "Match" filter and
    /// whose read of the very next row faults, so the background scan fails deterministically
    /// mid-batch — before the reveal threshold when <paramref name="matchedRows"/> is below
    /// it, after the threshold otherwise.
    /// </summary>
    private static DrillDownState CreateFaultingDrillDown(int matchedRows)
    {
        var schema = new TableSchema
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        var names = Enumerable.Range(0, matchedRows).Select(static i => $"Match{i}")
            .Concat(Enumerable.Range(0, 1000 - matchedRows).Select(static i => $"Other{i}"));
        var rows = names
            .Select(static (name, index) => new FocusedTableRow(Encoding.UTF8.GetBytes($"{{\"name\":\"{name}\"}}"), $"[{index}]"))
            .ToArray();
        return new DrillDownState(new FaultingDrillDownRows(rows, faultRowIndex: matchedRows), schema, ViewMode.JsonArrayTree, KeyPath: [], ActionStack: []);
    }

    /// <summary>
    /// Executes queued callbacks until the current view is a FocusedTable view whose scan
    /// overlay is gone — the terminal look of a completed staged scan, regardless of how many
    /// notifications the scan produced.
    /// </summary>
    private static async Task<FocusedTableView> WaitForCompletedFocusedTableViewAsync(
        ViewManager viewManager,
        System.Collections.Concurrent.ConcurrentQueue<Action> captured)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            ExecuteRemainingCallbacks(captured);
            if (viewManager.GetCurrentView() is FocusedTableView { } view && !view.SubViews.OfType<ProgressBar>().Any())
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
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Alice3").Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using var state = new AppState();
        state.EnterFocusedTable(drillDown);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        var captured = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), captured.Enqueue);

        // Act
        viewManager.SwitchToFocusedTable(drillDown);

        // Assert — one match is below the display threshold, so the loading view stays
        viewManager.GetCurrentView().Should().BeOfType<LoadingView>();
        var onProgress = await WaitForCallbackAsync(captured);
        onProgress();
        viewManager.GetCurrentView().Should().BeOfType<LoadingView>();

        var onCompleted = await WaitForCallbackAsync(captured);
        onCompleted();

        var view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(1);
        view.SubViews.OfType<ProgressBar>().Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFilterMatchingManyRows_RevealsTableFromThresholdNotification()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 500) with { ActionStack = [filter] };
        using var state = new AppState();
        state.EnterFocusedTable(drillDown);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        var captured = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), captured.Enqueue);

        // Act
        viewManager.SwitchToFocusedTable(drillDown);
        viewManager.GetCurrentView().Should().BeOfType<LoadingView>();

        // Assert — 500 matched rows exceed the threshold, so the reveal and the first tick
        // (queued in either order) both stage the table; only the completion stays pending
        await WaitForCallbacksAsync(captured, expectedCount: 3);
        var onReveal = await WaitForCallbackAsync(captured);
        onReveal();
        var onFirstTick = await WaitForCallbackAsync(captured);
        onFirstTick();

        var view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(500);
        view.SubViews.OfType<ProgressBar>().Should().ContainSingle();

        var onCompleted = await WaitForCallbackAsync(captured);
        onCompleted();
        view.SubViews.OfType<ProgressBar>().Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFilterAndCancelledToken_StaysOnLoadingViewWithoutCallbacks()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
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
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
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
        using var app = TestAppFactory.Create();
        var drillDown = CreateNumberedDrillDown(rowCount: 12);
        using var state = new AppState();
        state.EnterFocusedTable(drillDown);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        var captured = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), captured.Enqueue);
        viewManager.SwitchToFocusedTable(drillDown);
        captured.Clear();
        var viewBeforeFilter = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        viewBeforeFilter.SetSelection(0, 5, false);
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;

        // Act
        viewBeforeFilter.OnMorphAction?.Invoke(filter);
        var viewAfterFilter = await WaitForFocusedTableViewAsync(viewManager, captured);
        ExecuteRemainingCallbacks(captured);

        // Assert
        viewAfterFilter.Should().NotBeSameAs(viewBeforeFilter);
        AssertAtFirstPageAndRow(viewAfterFilter);
    }

    [Fact]
    public async Task RefreshCurrentTableView_AfterClearingFilterFromMovedSelection_ReturnsToFirstPageAndRow()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        var baseDrillDown = CreateNumberedDrillDown(rowCount: 12);
        var drillDown = baseDrillDown with { ActionStack = [filter] };
        using var state = new AppState();
        state.EnterFocusedTable(drillDown);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        var captured = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), captured.Enqueue);
        viewManager.SwitchToFocusedTable(drillDown);
        var viewBeforeClear = await WaitForFocusedTableViewAsync(viewManager, captured);
        ExecuteRemainingCallbacks(captured);
        viewBeforeClear.SetSelection(0, 5, false);
        state.ClearDrillDownActions();

        // Act
        viewManager.RefreshCurrentTableView();

        // Assert
        var viewAfterClear = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        viewAfterClear.Should().NotBeSameAs(viewBeforeClear);
        AssertAtFirstPageAndRow(viewAfterClear);
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFewerMatchesThanThresholdAndQueuedCallbacks_StaysOnLoadingViewUntilCompletionCallback()
    {
        // Arrange — 199 matches; the worker finishes before any queued callback runs (not a mid-scan test)
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = CreateMixedDrillDown(matchedInFirstBatch: 199, matchedInSecondBatch: 0)
            with
        { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        harness.State.EnterFocusedTable(drillDown);

        // Act — two batches queue two ticks (first + final) plus the completion
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 3);

        // Assert — neither tick reaches the display threshold, so the loading view stays
        var onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        harness.ViewManager.GetCurrentView().Should().BeOfType<LoadingView>();
        var onFinalTick = await WaitForCallbackAsync(harness.Captured);
        onFinalTick();
        harness.ViewManager.GetCurrentView().Should().BeOfType<LoadingView>();

        var onCompleted = await WaitForCallbackAsync(harness.Captured);
        onCompleted();

        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(199);
        view.SubViews.OfType<ProgressBar>().Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithThresholdMatchesAndQueuedCallbacks_RevealsTableFromThresholdAndGrowsRows()
    {
        // Arrange — 200 + 50 matches; callbacks are applied after the worker finished (not a mid-scan test)
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = CreateMixedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 50)
            with
        { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        harness.State.EnterFocusedTable(drillDown);

        // Act — the threshold reveal and both ticks queue (in any mutual order) before the completion
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 4);
        var onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        var onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        var onFinalTick = await WaitForCallbackAsync(harness.Captured);
        onFinalTick();

        // Assert — executing every pre-completion callback leaves the table staged with the
        // overlay attached; the live matched count by deferred-execution time is the final 250
        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(250);
        view.SubViews.OfType<ProgressBar>().Should().ContainSingle();
        AssertAtFirstPageAndRow(view);
        var transformer = GetFocusedTableTransformer(view);

        var onCompleted = await WaitForCallbackAsync(harness.Captured);
        onCompleted();
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(250);
        view.SubViews.OfType<ProgressBar>().Should().BeEmpty();

        // the completed path hands the transformer to the table view: it stays readable
        transformer[0, 1].Should().Be("Match0");
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithMoreMatchesThanThresholdAndQueuedCallbacks_RevealsTableFromQueuedCallbacks()
    {
        // Arrange — 201 matches; callbacks are applied after the worker finished (not a mid-scan test)
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = CreateMixedDrillDown(matchedInFirstBatch: 201, matchedInSecondBatch: 0)
            with
        { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        harness.State.EnterFocusedTable(drillDown);

        // Act — the threshold reveal and both ticks queue (in any mutual order) before the completion
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 4);
        var onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        var onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        var onFinalTick = await WaitForCallbackAsync(harness.Captured);
        onFinalTick();

        // Assert — executing every pre-completion callback reveals the table
        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(201);
        view.SubViews.OfType<ProgressBar>().Should().ContainSingle();
        AssertAtFirstPageAndRow(view);

        var onCompleted = await WaitForCallbackAsync(harness.Captured);
        onCompleted();
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(201);
        view.SubViews.OfType<ProgressBar>().Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFilter_AppliesWorkerNotificationsOnlyThroughUiThreadInvoke()
    {
        // Arrange — two batches: one threshold reveal plus two ticks plus one completion,
        // all queued by the worker
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = CreateMixedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 50)
            with
        { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        harness.State.EnterFocusedTable(drillDown);

        // Act
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        var loadingView = harness.ViewManager.GetCurrentView().Should().BeOfType<LoadingView>().Which;
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 4);

        // Assert — nothing the worker fired touches the view state before the UI thread invokes it
        harness.ViewManager.GetCurrentView().Should().BeSameAs(loadingView);
        loadingView.SubViews.OfType<ProgressBar>().Should().ContainSingle();

        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();

        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(250);
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithFewerMatchesThanThresholdWhileScanRunning_StaysOnLoadingView()
    {
        // Arrange — 199 matches in the first batch; the worker blocks on the second batch's first row
        using var app = TestAppFactory.Create();
        var gated = CreateGatedDrillDown(matchedInFirstBatch: 199, matchedInSecondBatch: 0, totalBatches: 2);
        using var rows = gated.Rows;
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = gated.DrillDown with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        await using var scope = new GatedScanScope(rows, harness);
        harness.State.EnterFocusedTable(drillDown);

        // Act — the first tick is queued while the worker is provably mid-scan
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();

        // Assert — 199 matches stay below the display threshold while the scan runs
        harness.ViewManager.GetCurrentView().Should().BeOfType<LoadingView>();

        rows.ReleaseGate();
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);
        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(199);
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithThresholdMatchesWhileScanRunning_RevealsTableAndGrowsRowsKeepingUserSelection()
    {
        // Arrange — 200 matches in the first batch, 50 more in the second; the worker blocks
        // on the second batch's first row
        using var app = TestAppFactory.Create();
        var gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 50, totalBatches: 2);
        using var rows = gated.Rows;
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = gated.DrillDown with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        await using var scope = new GatedScanScope(rows, harness);
        harness.State.EnterFocusedTable(drillDown);

        // Act — once the worker is blocked, exactly two batch-1 notifications are queued:
        // the threshold reveal and the first tick, in whichever order the threads landed
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);

        // Assert — the mid-scan reveal shows exactly the threshold rows with the overlay attached
        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(200);
        view.SubViews.OfType<ProgressBar>().Should().ContainSingle();
        AssertAtFirstPageAndRow(view);

        // the user moves off the first row, then the scan resumes and finds 50 more matches;
        // every remaining notification is applied while the completed look is awaited
        view.SetSelection(0, 5, false);
        rows.ReleaseGate();
        await WaitForCompletedFocusedTableViewAsync(harness.ViewManager, harness.Captured);

        // rows grew to 250 without moving the user's selection, and the scan retired the overlay
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(250);
        view.Value.Should().NotBeNull().And.Subject.As<TableSelection>().SelectedCell.Y.Should().Be(5);
        view.SubViews.OfType<ProgressBar>().Should().BeEmpty();
        harness.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithMoreMatchesThanThresholdWhileScanRunning_RevealsTableMidScan()
    {
        // Arrange — 201 matches in the first batch, none in the second; the worker blocks on
        // the second batch's first row
        using var app = TestAppFactory.Create();
        var gated = CreateGatedDrillDown(matchedInFirstBatch: 201, matchedInSecondBatch: 0, totalBatches: 2);
        using var rows = gated.Rows;
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = gated.DrillDown with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        await using var scope = new GatedScanScope(rows, harness);
        harness.State.EnterFocusedTable(drillDown);

        // Act — once the worker is blocked, exactly two batch-1 notifications are queued:
        // the threshold reveal and the first tick, in whichever order the threads landed
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);

        // Assert — the mid-scan reveal shows the 201 matched rows with the overlay attached
        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(201);
        view.SubViews.OfType<ProgressBar>().Should().ContainSingle();
        AssertAtFirstPageAndRow(view);

        rows.ReleaseGate();
        await WaitForCompletedFocusedTableViewAsync(harness.ViewManager, harness.Captured);
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(201);
        view.SubViews.OfType<ProgressBar>().Should().BeEmpty();
        harness.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WhenParentTokenCancelledAfterWorkerFinished_DiscardsQueuedCallbacksAndDisposeIsSafe()
    {
        // Arrange — 500 matches: the reveal, tick and completion queue up, none executed, so
        // the session is still current when the parent token is canceled
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 500) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        var loadingView = harness.ViewManager.GetCurrentView().Should().BeOfType<LoadingView>().Which;
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 3);

        // Act — canceling the parent token cancels the linked session token
        harness.State.Cts.Cancel();
        ExecuteRemainingCallbacks(harness.Captured);

        // Assert — no table reveal, no redraw, no selection reset, no overlay dismissal
        harness.ViewManager.GetCurrentView().Should().BeSameAs(loadingView);
        loadingView.SubViews.OfType<ProgressBar>().Should().ContainSingle();
        harness.Captured.Should().BeEmpty();

        var disposeAfterParentCancel = () => harness.ViewManager.Dispose();
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
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = CreateFaultingDrillDown(matchedRows) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        harness.State.EnterFocusedTable(drillDown);

        // Act — the row read faults mid-batch; once the recorded task ends, the queued
        // callbacks are the reveal (when the threshold fired) and the failure, in that order
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        var assertScan = () => harness.Runner.TaskAt(0);
        await assertScan.Should().ThrowAsync<InvalidOperationException>();
        await WaitForCallbacksAsync(harness.Captured, expectedCount: expectedCallbacks);
        ExecuteRemainingCallbacks(harness.Captured);

        // Assert — the failure retires the session and replaces the scan's views with the
        // error placeholder; no overlay survives, nothing stays queued, disposal stays safe
        harness.State.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        var placeholder = harness.ViewManager.GetCurrentView().Should().BeOfType<PlaceholderView>().Which;
        placeholder.Text.Should().Contain("row read failed");
        placeholder.SubViews.OfType<ProgressBar>().Should().BeEmpty();
        harness.Captured.Should().BeEmpty();
        var disposeAfterFailure = () => harness.ViewManager.Dispose();
        disposeAfterFailure.Should().NotThrow();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WhenScanFaultsAfterThreshold_DisposesRevealedTransformer()
    {
        // Arrange — 250 matches reveal the table before the row read faults
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = CreateFaultingDrillDown(250) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        harness.State.EnterFocusedTable(drillDown);

        // Act — the reveal is enqueued before the fault's callback, so executing it first
        // stages the table; the failure callback then retires the session
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        var assertScan = () => harness.Runner.TaskAt(0);
        await assertScan.Should().ThrowAsync<InvalidOperationException>();
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        var onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        var transformer = GetFocusedTableTransformer(
            harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which);
        ExecuteRemainingCallbacks(harness.Captured);

        // Assert — the error placeholder is up and the session released the transformer
        harness.ViewManager.GetCurrentView().Should().BeOfType<PlaceholderView>();
        var readDisposedTransformer = () => transformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
        harness.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchToFocusedTable_WithStaleFailureCallbackAfterReturn_KeepsRestoredTree()
    {
        // Arrange — JsonArrayTree backing data so the drill-down can return to the tree
        var filePath = CreateTempFile(".json", "[1,2,3]");
        using var app = TestAppFactory.Create();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = CreateFaultingDrillDown(matchedRows: 50) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
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
        using var app = TestAppFactory.Create();
        var gated = CreateGatedDrillDown(
            matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 2, gateRowIndex: 200);
        using var rows = gated.Rows;
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = gated.DrillDown with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        await using var scope = new GatedScanScope(rows, harness);
        harness.State.EnterFocusedTable(drillDown);

        // Act — while the worker is provably blocked, the threshold reveal is the only callback
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 1);
        ExecuteRemainingCallbacks(harness.Captured);

        // Assert — the table is revealed mid-scan, before any progress tick exists
        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(200);
        view.SubViews.OfType<ProgressBar>().Should().ContainSingle();
        AssertAtFirstPageAndRow(view);

        // teardown — release the scan and let its remaining notifications drain
        rows.ReleaseGate();
        await WaitForCompletedFocusedTableViewAsync(harness.ViewManager, harness.Captured);
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(200);
        view.SubViews.OfType<ProgressBar>().Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshCurrentTableView_WithClearedFilterAndQueuedScanCallbacks_IgnoresStaleCallbacks()
    {
        // Arrange — one match: its tick and completion queue up while the loading view stays
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Alice3").Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);

        // Act — clearing the filter rebuilds the view directly, without a new scan
        harness.State.ClearDrillDownActions();
        harness.ViewManager.RefreshCurrentTableView();

        // Assert — the stale tick and completion leave the new unfiltered view untouched
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(12);
    }

    [Fact]
    public async Task RefreshCurrentTableView_WithAppendedActionAndQueuedScanCallbacks_IgnoresStaleCallbacks()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Alice3").Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
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
        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(1);
        view.Table.ColumnNames.Should().Contain("label (text)");
    }

    [Fact]
    public async Task ReturnFromDrillDown_WithQueuedScanCallbacks_IgnoresStaleCallbacks()
    {
        // Arrange — JsonArrayTree backing data so the drill-down can return to the tree
        var filePath = CreateTempFile(".json", "[1,2,3]");
        using var app = TestAppFactory.Create();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        var filter = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Alice3").Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
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
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Alice3").Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 12) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
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
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 500) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2); // threshold reveal + first tick
        var onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        var onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        var stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        var oldTransformer = GetFocusedTableTransformer(stagedView);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 1); // stale completion queued
        var replacement = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Alice3").Value;

        // Act — replacing the filter cancels the old scan and starts a new one
        stagedView.OnMorphAction?.Invoke(replacement);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 3); // stale completion + 2 from the new scan

        // Assert — the old session is torn down and its stale completion is inert
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        var readDisposedTransformer = () => oldTransformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();

        var newView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        newView.Should().NotBeSameAs(stagedView);
        newView.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(1);
    }

    [Fact]
    public async Task HandleMorphAction_WithTransformActionAfterScanFinished_IgnoresQueuedCompletionAndDisposesOldTransformer()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 500) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2); // threshold reveal + first tick
        var onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        var onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        var stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        var oldTransformer = GetFocusedTableTransformer(stagedView);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 1);

        // Act — a transform action rebuilds the transformer and restarts the scan; the new
        // scan matches 500 rows again, so it queues its own reveal and tick before completing
        stagedView.OnMorphAction?.Invoke(new RenameColumnAction { OldName = "name", NewName = "label" });
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 4); // stale completion + 3 from the new scan

        // Assert
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        var readDisposedTransformer = () => oldTransformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();

        var newView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        newView.Should().NotBeSameAs(stagedView);
        newView.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(500);
        newView.Table.ColumnNames.Should().Contain("label (text)");
    }

    [Fact]
    public async Task ReturnFromDrillDown_AfterScanFinishedWithStagedTable_IgnoresQueuedCompletionAndDisposesOldTransformer()
    {
        // Arrange
        var filePath = CreateTempFile(".json", "[1,2,3]");
        using var app = TestAppFactory.Create();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 500) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        harness.State.StartNewFile(filePath);
        harness.State.BeginIndexedTreeLoad(indexer);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2); // threshold reveal + first tick
        var onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        var onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        var stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        var oldTransformer = GetFocusedTableTransformer(stagedView);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 1);

        // Act
        harness.ViewManager.ReturnFromDrillDown();

        // Assert
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        harness.ViewManager.GetCurrentView().Should().BeOfType<JsonArrayTreeView>();
        var readDisposedTransformer = () => oldTransformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_AfterScanFinishedWithStagedTable_IgnoresQueuedCompletionAndIsIdempotent()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Alice").Value;
        var drillDown = CreateNumberedDrillDown(rowCount: 500) with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2); // threshold reveal + first tick
        var onReveal = await WaitForCallbackAsync(harness.Captured);
        onReveal();
        var onFirstTick = await WaitForCallbackAsync(harness.Captured);
        onFirstTick();
        var stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        var transformer = GetFocusedTableTransformer(stagedView);
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 1);

        // Act
        harness.ViewManager.Dispose();
        harness.ViewManager.Dispose();

        // Assert — the session-owned transformer is disposed and stale callbacks stay inert
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        var readDisposedTransformer = () => transformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task RefreshCurrentTableView_WithClearedFilterWhileWorkerBlocked_StopsWorkerAfterInFlightBatch()
    {
        // Arrange — three batches; the batch 1 notifications stay queued (loading view) and
        // the worker is blocked on the first row of batch 2
        using var app = TestAppFactory.Create();
        var gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using var rows = gated.Rows;
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = gated.DrillDown with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        await using var scope = new GatedScanScope(rows, harness);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; waiting for both keeps the later drain deterministic
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);

        // Act — clearing the filter cancels the blocked scan; no new scan starts
        harness.State.ClearDrillDownActions();
        harness.ViewManager.RefreshCurrentTableView();
        rows.ReleaseGate();

        // Assert — the recorded scan task ends by cancellation rather than completion, which
        // deterministically proves the worker stopped after batches 1–2 (never reading batch 3)
        var assertOldScan = () => harness.Runner.TaskAt(0);
        await assertOldScan.Should().ThrowAsync<OperationCanceledException>();
        rows.ReadCount.Should().Be(2000);

        // the stale notifications leave the new unfiltered view untouched
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(3000);
    }

    [Fact]
    public async Task ReturnFromDrillDown_WhileWorkerBlocked_StopsWorkerAfterInFlightBatch()
    {
        // Arrange — JsonArrayTree backing data so the drill-down can return to the tree
        var filePath = CreateTempFile(".json", "[1,2,3]");
        using var app = TestAppFactory.Create();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        var gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using var rows = gated.Rows;
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = gated.DrillDown with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        await using var scope = new GatedScanScope(rows, harness);
        harness.State.StartNewFile(filePath);
        harness.State.BeginIndexedTreeLoad(indexer);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; waiting for both keeps the later drain deterministic
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);

        // Act — returning to the tree cancels the blocked scan
        harness.ViewManager.ReturnFromDrillDown();
        rows.ReleaseGate();

        // Assert — the recorded scan task ends by cancellation after batches 1–2 only
        var assertOldScan = () => harness.Runner.TaskAt(0);
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
        using var app = TestAppFactory.Create();
        var gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using var rows = gated.Rows;
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = gated.DrillDown with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        await using var scope = new GatedScanScope(rows, harness);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; waiting for both keeps the later drain deterministic
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);

        // Act — disposing mid-scan cancels the blocked worker; the second call must be safe
        harness.ViewManager.Dispose();
        harness.ViewManager.Dispose();
        rows.ReleaseGate();

        // Assert — the recorded scan task ends by cancellation after batches 1–2 only, and
        // the queued notifications stay inert
        var assertOldScan = () => harness.Runner.TaskAt(0);
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
        using var app = TestAppFactory.Create();
        var gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using var rows = gated.Rows;
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = gated.DrillDown with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        await using var scope = new GatedScanScope(rows, harness);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; execute them in whichever order the threads landed
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);
        var stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        stagedView.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(200);
        var oldTransformer = GetFocusedTableTransformer(stagedView);
        var replacement = FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "Match0").Value;

        // Act — replacing the filter cancels the blocked old worker and starts a new scan;
        // the first recorded task is the old worker's, and it must end by cancellation
        stagedView.OnMorphAction?.Invoke(replacement);
        rows.ReleaseGate();
        var assertOldScan = () => harness.Runner.TaskAt(0);
        await assertOldScan.Should().ThrowAsync<OperationCanceledException>();
        var newView = await WaitForCompletedFocusedTableViewAsync(harness.ViewManager, harness.Captured);

        // Assert — the old session is torn down and the replacement scan produced the new view
        harness.Captured.Should().BeEmpty();
        var readDisposedTransformer = () => oldTransformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
        newView.Should().NotBeSameAs(stagedView);
        newView.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(1);
    }

    [Fact]
    public async Task HandleMorphAction_WithTransformActionWhileWorkerBlocked_StopsOldWorkerAndDisposesOldTransformer()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using var rows = gated.Rows;
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = gated.DrillDown with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        await using var scope = new GatedScanScope(rows, harness);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; execute them in whichever order the threads landed
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);
        var stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        var oldTransformer = GetFocusedTableTransformer(stagedView);

        // Act — a transform action cancels the blocked old worker and restarts the scan; the
        // first recorded task is the old worker's, and it must end by cancellation
        stagedView.OnMorphAction?.Invoke(new RenameColumnAction { OldName = "name", NewName = "label" });
        rows.ReleaseGate();
        var assertOldScan = () => harness.Runner.TaskAt(0);
        await assertOldScan.Should().ThrowAsync<OperationCanceledException>();
        var newView = await WaitForCompletedFocusedTableViewAsync(harness.ViewManager, harness.Captured);

        // Assert — no queue-drain assertion here: the new scan also crosses the threshold, so
        // its reveal can legally be queued after the completion the helper waited for
        var readDisposedTransformer = () => oldTransformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
        newView.Should().NotBeSameAs(stagedView);
        newView.Table.Should().BeOfType<PagedTableSource>().Which.Rows.Should().Be(200);
        newView.Table.ColumnNames.Should().Contain("label (text)");
    }

    [Fact]
    public async Task ReturnFromDrillDown_WhileWorkerBlockedOnStagedTable_StopsWorkerAndDisposesOldTransformer()
    {
        // Arrange
        var filePath = CreateTempFile(".json", "[1,2,3]");
        using var app = TestAppFactory.Create();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        var gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using var rows = gated.Rows;
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = gated.DrillDown with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        await using var scope = new GatedScanScope(rows, harness);
        harness.State.StartNewFile(filePath);
        harness.State.BeginIndexedTreeLoad(indexer);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; execute them in whichever order the threads landed
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);
        var stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        var oldTransformer = GetFocusedTableTransformer(stagedView);

        // Act — returning to the tree cancels the blocked scan; the recorded task must end
        // by cancellation after batches 1–2 only
        harness.ViewManager.ReturnFromDrillDown();
        rows.ReleaseGate();
        var assertOldScan = () => harness.Runner.TaskAt(0);
        await assertOldScan.Should().ThrowAsync<OperationCanceledException>();
        rows.ReadCount.Should().Be(2000);

        // Assert — the worker never read batch 3 and the old transformer is disposed
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        harness.ViewManager.GetCurrentView().Should().BeOfType<JsonArrayTreeView>();
        var readDisposedTransformer = () => oldTransformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_WhileWorkerBlockedOnStagedTable_StopsWorkerDisposesSessionAndIsIdempotent()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using var rows = gated.Rows;
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = gated.DrillDown with { ActionStack = [filter] };
        using var harness = new FilterScanHarness();
        await using var scope = new GatedScanScope(rows, harness);
        harness.State.EnterFocusedTable(drillDown);
        harness.ViewManager.SwitchToFocusedTable(drillDown);
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // both batch-1 notifications (threshold reveal and first tick) are queued once the
        // worker is blocked; execute them in whichever order the threads landed
        await WaitForCallbacksAsync(harness.Captured, expectedCount: 2);
        ExecuteRemainingCallbacks(harness.Captured);
        var stagedView = harness.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        var transformer = GetFocusedTableTransformer(stagedView);

        // Act — disposing mid-scan cancels the blocked worker; the second call must be safe;
        // the recorded task must end by cancellation after batches 1–2 only
        harness.ViewManager.Dispose();
        harness.ViewManager.Dispose();
        rows.ReleaseGate();
        var assertOldScan = () => harness.Runner.TaskAt(0);
        await assertOldScan.Should().ThrowAsync<OperationCanceledException>();
        rows.ReadCount.Should().Be(2000);

        // Assert — the worker never read batch 3 and the session-owned transformer is disposed
        ExecuteRemainingCallbacks(harness.Captured);
        harness.Captured.Should().BeEmpty();
        var readDisposedTransformer = () => transformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task FilterScanManagerDispose_CalledTwiceWithActiveScan_CancelsScanDisposesSessionAndDoesNotThrow()
    {
        // Arrange — the manager is driven directly, so only its own guard can make the
        // second Dispose safe (ViewManager.Dispose short-circuits repeated calls)
        using var app = TestAppFactory.Create();
        var gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using var rows = gated.Rows;
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = gated.DrillDown with { ActionStack = [filter] };
        using var transformer = FocusedTableTransformer.Create(
            new FocusedTableSource(drillDown),
            drillDown.Schema,
            drillDown.ActionStack,
            200);
        var indexer = transformer.FilterRowIndexer.Should().BeOfType<FocusedTableFilterRowIndexer>().Subject;
        var runner = new RecordingFilterScanRunner();
        using var parentCts = new CancellationTokenSource();
        var captured = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        await using var scope = new DirectScanScope(rows, runner);
        var manager = new FilterScanManager(
            runner,
            captured.Enqueue,
            () => parentCts.Token,
            static (_, _) => throw new InvalidOperationException("The table view must not be created in this test."),
            view => scope.LoadingView = view,
            static () => { },
            static _ => { },
            static () => null);
        scope.Manager = manager;
        manager.StartScan(transformer, indexer, new TableStyle { AlwaysShowHeaders = true });
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Act
        manager.Dispose();
        var disposeAgain = () => manager.Dispose();
        rows.ReleaseGate();

        // Assert — the worker observed the cancellation after batches 1–2 only, and the
        // session-owned transformer is disposed
        disposeAgain.Should().NotThrow();
        var assertScan = () => runner.TaskAt(0);
        await assertScan.Should().ThrowAsync<OperationCanceledException>();
        rows.ReadCount.Should().Be(2000);
        var readDisposedTransformer = () => transformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
        scope.BodyCompleted = true;
    }

    [Fact]
    public async Task FilterScanManagerProgress_WhenCurrentSessionIsCanceled_DoesNotUpdateOverlay()
    {
        // Arrange — the session stays the current one; only its CTS is canceled (parent token)
        using var app = TestAppFactory.Create();
        var gated = CreateGatedDrillDown(matchedInFirstBatch: 200, matchedInSecondBatch: 0, totalBatches: 3);
        using var rows = gated.Rows;
        var filter = FilterAction.Create("name", FilterOperator.Contains, ComparisonType.Text, "Match").Value;
        var drillDown = gated.DrillDown with { ActionStack = [filter] };
        using var transformer = FocusedTableTransformer.Create(
            new FocusedTableSource(drillDown),
            drillDown.Schema,
            drillDown.ActionStack,
            200);
        var indexer = transformer.FilterRowIndexer.Should().BeOfType<FocusedTableFilterRowIndexer>().Subject;
        var runner = new RecordingFilterScanRunner();
        using var parentCts = new CancellationTokenSource();
        var captured = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        await using var scope = new DirectScanScope(rows, runner);
        var manager = new FilterScanManager(
            runner,
            captured.Enqueue,
            () => parentCts.Token,
            static (_, _) => throw new InvalidOperationException("The table view must not be created in this test."),
            view => scope.LoadingView = view,
            static () => { },
            static _ => { },
            static () => null);
        scope.Manager = manager;
        manager.StartScan(transformer, indexer, new TableStyle { AlwaysShowHeaders = true });
        await rows.GateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // the threshold reveal and the first progress tick are both queued once the worker is blocked
        await WaitForCallbacksAsync(captured, expectedCount: 2);
        var loadingView = scope.LoadingView.Should().BeAssignableTo<Terminal.Gui.ViewBase.View>().Subject;
        var bar = loadingView.SubViews.OfType<ProgressBar>().Should().ContainSingle().Subject;
        var label = loadingView.SubViews.OfType<Label>().Where(static l => l.Text.Contains('%', StringComparison.Ordinal)).Should().ContainSingle().Subject;
        var fractionBefore = bar.Fraction;
        var textBefore = label.Text;

        // Act
        parentCts.Cancel();
        ExecuteRemainingCallbacks(captured);

        // Assert
        bar.Fraction.Should().Be(fractionBefore);
        label.Text.Should().Be(textBefore);
        scope.BodyCompleted = true;
    }

    [Fact]
    public void HandleMorphAction_WithFocusedTableModeAndDrillDown_AppendsToDrillDownActionStack()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var schema = new TableSchema
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
        using var state = new AppState();
        state.EnterFocusedTable(drillDown);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.SwitchToFocusedTable(drillDown);
        var view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        var action = new RenameColumnAction { OldName = "name", NewName = "label" };

        // Act
        view.OnMorphAction?.Invoke(action);

        // Assert
        state.ActionStack.Should().BeEmpty();
        var drillDownAfter = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
        drillDownAfter.ActionStack.Should().Equal(action);
    }

    [Fact]
    public void HandleMorphAction_WithFocusedTableModeAndDrillDown_MarksDrillDownSessionUnsaved()
    {
        // Arrange — the DrillDown replays a just-loaded recipe (clean); applying an action within
        // it must flip its own flag without touching the root one
        using var app = TestAppFactory.Create();
        var schema = new TableSchema
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
        using var state = new AppState();
        state.EnterFocusedTable(drillDown);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.SwitchToFocusedTable(drillDown);
        var view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        var action = new RenameColumnAction { OldName = "name", NewName = "label" };

        // Act
        view.OnMorphAction?.Invoke(action);

        // Assert
        var drillDownAfter = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
        drillDownAfter.HasUnsavedChanges.Should().BeTrue();
        state.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void HandleMorphAction_WithFocusedTableModeAndBaseActions_LeavesBaseStackUntouched()
    {
        // Arrange — a populated base ActionStack must survive appending a morph action to the
        // active DrillDown's own stack
        using var app = TestAppFactory.Create();
        var baseAction = new RenameColumnAction { OldName = "base", NewName = "renamed_base" };
        var schema = new TableSchema
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
        using var state = new AppState();
        state.EnterFocusedTable(drillDown);
        state.AddMorphAction(baseAction);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.SwitchToFocusedTable(drillDown);
        var view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        var drillDownAction = new DeleteColumnAction { ColumnName = "drill_down" };

        // Act
        view.OnMorphAction?.Invoke(drillDownAction);

        // Assert
        state.ActionStack.Should().Equal(baseAction);
        var drillDownAfter = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
        drillDownAfter.ActionStack.Should().Equal(drillDownAction);
    }

    [Fact]
    public void HandleMorphAction_AfterErrorViewReplacedFocusedTable_AppendsToBaseActionStack()
    {
        // Arrange — an error view replaced FocusedTable, then the base table was shown again;
        // the action must go to the base stack and no DrillDown session may remain
        var filePath = CreateTempFile(".csv", "col1,col2\nvalue1,value2\n");
        using var app = TestAppFactory.Create();
        var drillDownAction = new RenameColumnAction { OldName = "x", NewName = "y" };
        var drillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] },
            ViewMode.JsonLinesTree,
            KeyPath: [],
            ActionStack: [drillDownAction]);
        using var state = new AppState();
        state.StartNewFile(filePath);
        state.EnterFocusedTable(drillDown);
        state.EnterPlaceholderMode();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        var schema = new TableSchema
        {
            SourceFormat = DataFormat.Csv,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }],
        };
        viewManager.SwitchToCsvTable(new MockRowIndexer(filePath), schema);
        var view = viewManager.GetCurrentView().Should().BeOfType<CsvTableView>().Which;
        var action = new RenameColumnAction { OldName = "col1", NewName = "renamed" };

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
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        var request = new SingleDrillDownRequest(
            DataFormat.JsonObject,
            Encoding.UTF8.GetBytes("[{\"a\":1}]"),
            [new KeyPathSegment("list", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        viewManager.DrillDown(request);

        // Assert
        state.ActionStack.Should().ContainSingle();
        var drillDown = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
        drillDown.ActionStack.Should().BeEmpty();
    }

    [Fact]
    public void DrillDown_OnFailure_LeavesActionStackUntouched()
    {
        // Arrange — an empty array node fails schema extraction, routing to ShowError
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        var request = new SingleDrillDownRequest(
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
        var filePath = CreateTempFile(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        state.AddMorphAction(new RenameColumnAction { OldName = "name", NewName = "label" });
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        var request = new FullAggregationDrillDownRequest(
            DataFormat.JsonLines,
            [new KeyPathSegment("user", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        await viewManager.FullAggregationDrillDownAsync(request);

        // Assert
        state.ActionStack.Should().ContainSingle();
        var drillDown = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
        drillDown.ActionStack.Should().BeEmpty();
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_OnFailure_LeavesActionStackUntouched()
    {
        // Arrange — a non-matching KeyPath forces the scan to fail
        var filePath = CreateTempFile(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.StartNewFile(filePath);
        state.AddMorphAction(new RenameColumnAction { OldName = "name", NewName = "label" });
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

        var request = new FullAggregationDrillDownRequest(
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
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"value\"}\n");
        using var app = TestAppFactory.Create();
        var indexer = new Refedle.Engine.IO.JsonLines.RowIndexer(filePath);
        indexer.BuildIndex();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState();
        state.StartNewFile(filePath);
        state.BeginIndexedTreeLoad(indexer);
        var drillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonLinesTree, KeyPath: [], ActionStack: []);
        state.EnterFocusedTable(drillDown);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

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
        var filePath = CreateTempFile(".json", "[1,2,3]");
        using var app = TestAppFactory.Create();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonArray, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState();
        state.StartNewFile(filePath);
        state.BeginIndexedTreeLoad(indexer);
        var drillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonArrayTree, KeyPath: [], ActionStack: []);
        state.EnterFocusedTable(drillDown);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

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
        using var app = TestAppFactory.Create();
        IReadOnlyList<JsonObjectEntry> entries = [new JsonObjectEntry("id", Encoding.UTF8.GetBytes("1"))];
        var schema = new TableSchema { SourceFormat = DataFormat.JsonObject, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState();
        state.StartNewFile("test.json");
        state.EnterJsonObjectTree(entries);
        var drillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonObjectTree, KeyPath: [], ActionStack: []);
        state.EnterFocusedTable(drillDown);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());

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
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
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
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        var viewManager = new ViewManager(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.Dispose();

        // Act
        var act = () => viewManager.ReturnFromDrillDown();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    /// <summary>
    /// Mock IRowIndexer for testing.
    /// </summary>
    private sealed class MockRowIndexer(string filePath, long totalRows = 10) : IRowIndexer
    {
        public string FilePath => filePath;
        public long FileSize => 1000;
        public long BytesRead => 1000;
        public long TotalRows => totalRows;
        public bool IsIndexingCompleted => true;

#pragma warning disable CS0067
        public event Action? FirstCheckpointReached;
        public event Action<long, long>? ProgressChanged;
        public event Action? BuildIndexCompleted;
#pragma warning restore CS0067

        public void BuildIndex(CancellationToken cancellationToken = default) { }

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => (0, 0);
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
        public RecordingFilterScanRunner Runner { get; } = new();
        public ViewManager ViewManager { get; }

        public FilterScanHarness()
        {
            var modeController = new ModeController(State, action => action(), TestSchemaScannerFactories.JsonLines);
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
    /// <see cref="IFilterScanRunner"/> double that delegates to a real
    /// <see cref="FilterIndexingRunner"/> while recording each started task in start order,
    /// so a test can await a specific scan's task and observe how it terminated —
    /// a deterministic replacement for read-count probing of the gated rows.
    /// </summary>
    private sealed class RecordingFilterScanRunner : IFilterScanRunner
    {
        private sealed record Run(IFilterRowIndexer Indexer, Task Task);

        private readonly FilterIndexingRunner _inner = new();
        private readonly Lock _lock = new();
        private readonly List<Run> _runs = [];

        public Task RunAsync(IFilterRowIndexer filterIndexer, CancellationToken ct)
        {
            var task = _inner.RunAsync(filterIndexer, ct);
            lock (_lock)
            {
                _runs.Add(new Run(filterIndexer, task));
            }

            return task;
        }

        /// <summary>The task of the scan started at <paramref name="index"/>, in start order.</summary>
        public Task TaskAt(int index)
        {
            lock (_lock)
            {
                return _runs[index].Task;
            }
        }

        /// <summary>Snapshot of every started scan task, in start order.</summary>
        public IReadOnlyList<Task> Tasks()
        {
            lock (_lock)
            {
                return [.. _runs.Select(static run => run.Task)];
            }
        }
    }

    /// <summary>
    /// Row list whose read of one configured row blocks until released, so ViewManager-level
    /// tests can hold the filter-scan worker mid-batch. Every cell read of a row — the "#"
    /// column and data columns alike — goes through this indexer, and reads are counted so a
    /// test can prove the worker stopped reading further batches.
    /// </summary>
    private sealed class GatedDrillDownRows(FocusedTableRow[] rows, int gateRowIndex)
        : IReadOnlyList<FocusedTableRow>, IDisposable
    {
        private readonly ManualResetEventSlim _gate = new(false);
        private long _readCount;

        public TaskCompletionSource GateReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Count => rows.Length;

        public long ReadCount => Interlocked.Read(ref _readCount);

        public FocusedTableRow this[int index]
        {
            get
            {
                Interlocked.Increment(ref _readCount);
                if (index == gateRowIndex)
                {
                    GateReached.TrySetResult();
                    _gate.Wait();
                }

                return rows[index];
            }
        }

        public IEnumerator<FocusedTableRow> GetEnumerator() => ((IReadOnlyList<FocusedTableRow>)rows).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            rows.GetEnumerator();

        public void ReleaseGate() => _gate.Set();

        public void Dispose() => _gate.Dispose();
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
            foreach (var task in harness.Runner.Tasks())
            {
                // WhenAny observes a canceled or faulted worker without throwing; a worker
                // stuck past the bound fails the test instead of hanging cleanup
                await Task.WhenAny(task.WaitAsync(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
                task.IsCompleted.Should().BeTrue("a scan worker must terminate within the bounded cleanup wait");
            }
        }
    }

    /// <summary>
    /// Cleanup scope for tests that drive a <see cref="FilterScanManager"/> directly: on every
    /// exit it releases the gate, disposes the manager, joins the started workers with a
    /// bounded wait and disposes the loading view, running every step even if one fails.
    /// Cleanup failures surface only after the test body completed (<see cref="BodyCompleted"/>);
    /// otherwise they are dropped so they cannot replace the failure already propagating.
    /// </summary>
    private sealed class DirectScanScope(GatedDrillDownRows rows, RecordingFilterScanRunner runner) : IAsyncDisposable
    {
        public FilterScanManager? Manager { get; set; }

        public Terminal.Gui.ViewBase.View? LoadingView { get; set; }

        public bool BodyCompleted { get; set; }

        public async ValueTask DisposeAsync()
        {
            List<Exception> failures = [];
            Capture(failures, rows.ReleaseGate);
            Capture(failures, () => Manager?.Dispose());
            foreach (var task in runner.Tasks())
            {
                // WhenAny observes a canceled or faulted worker without throwing
                await Task.WhenAny(task.WaitAsync(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
                if (!task.IsCompleted)
                {
                    failures.Add(new TimeoutException("A scan worker did not terminate within the bounded cleanup wait."));
                }
            }

            Capture(failures, () => LoadingView?.Dispose());
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
}

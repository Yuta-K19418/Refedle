using System.Text;
using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Views;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonArray;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace Refedle.Tests.App;

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

    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        Assert.NotNull(app.Driver);
        app.Driver.SetScreenSize(80, 25);
        return app;
    }

    [Fact]
    public void RefreshStatusBarHints_WithNoFilePath_UsesDefaultHints()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = string.Empty };
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath };
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath };
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath };
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath };
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath, CurrentMode = ViewMode.FocusedTable };
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

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
        using var app = CreateTestApp();
        using var state = new AppState
        {
            CurrentFilePath = string.Empty,
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = new MockRowIndexer("test.jsonl")
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath, CurrentMode = ViewMode.JsonLinesTable };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        // Setup a valid table state
        var schema = new TableSchema
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };
        state.Schema = schema;
        state.RowIndexer = new MockRowIndexer(filePath);

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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath, CurrentMode = ViewMode.JsonLinesTree };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        // Setup a valid tree state
        var schema = new TableSchema
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };
        state.Schema = schema;
        state.RowIndexer = new MockRowIndexer(filePath);

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
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

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
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        var viewManager = new ViewManager(window, state, modeController, action => action());
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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath, CurrentMode = ViewMode.JsonArrayTree };
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath, RowIndexer = new MockRowIndexer("test.csv", 5000) };
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        viewManager.RefreshStatusBarHints();
        window.SubViews.OfType<Label>().First(l => l.Text == "5000 items").Text.Should().Be("5000 items");

        // Act — clear RowIndexer so the label won't be re-added
        state.RowIndexer = null;
        viewManager.RefreshStatusBarHints();

        // Assert
        window.SubViews.OfType<Label>().Should().NotContain(l => l.Text == "5000 items");
    }

    [Fact]
    public void RefreshStatusBarHints_WithCompletedIndexer_ShowsItemCountLabel()
    {
        // Arrange
        var filePath = CreateTempFile(".csv", "col1\nval1");
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath, RowIndexer = new MockRowIndexer("test.csv", 5000) };
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath, RowIndexer = new MockRowIndexer("test.csv", 1000) };
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        // Act — first call
        viewManager.RefreshStatusBarHints();

        // Assert — shows count from first indexer
        window.SubViews.OfType<Label>().First(l => l.Text == "1000 items").Text.Should().Be("1000 items");

        // Act — replace indexer with one that has a different row count
        state.RowIndexer = new MockRowIndexer("test.csv", 5000);
        viewManager.RefreshStatusBarHints();

        // Assert — shows updated count, old count is gone
        window.SubViews.OfType<Label>().First(l => l.Text == "5000 items").Text.Should().Be("5000 items");
        window.SubViews.OfType<Label>().Should().NotContain(l => l.Text == "1000 items");
    }

    [Fact]
    public void SwitchToJsonArrayTree_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
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
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        var viewManager = new ViewManager(window, state, modeController, action => action());
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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = "test.json" };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
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
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
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
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        var viewManager = new ViewManager(window, state, modeController, action => action());
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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath, CurrentMode = ViewMode.JsonObjectTree };
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        IReadOnlyList<JsonObjectEntry> entries =
        [
            new JsonObjectEntry("id", System.Text.Encoding.UTF8.GetBytes("1")),
        ];
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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        var request = new FullAggregationDrillDownRequest(
            DataFormat.JsonLines,
            [new KeyPathSegment("user", KeyPathSegmentKind.Key)]);

        // Act — immediate-execution uiThreadInvoke applies the scanned result synchronously
        await viewManager.FullAggregationDrillDownAsync(request);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        state.DrillDown.Should().NotBeNull();
        viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>();
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WhenScanFails_ShowsErrorPlaceholder()
    {
        // Arrange — real file + non-matching KeyPath, so the sealed ModeController's scan fails.
        var filePath = CreateTempFile(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        var request = new FullAggregationDrillDownRequest(
            DataFormat.JsonLines,
            [new KeyPathSegment("missing", KeyPathSegmentKind.Key)]);

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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath };
        using var window = new Window();
        var modeController = new ModeController(state);

        List<Action> capturedCallbacks = [];
        using var viewManager = new ViewManager(
            window, state, modeController, cb => capturedCallbacks.Add(cb));

        var request = new FullAggregationDrillDownRequest(
            DataFormat.JsonLines,
            [new KeyPathSegment("user", KeyPathSegmentKind.Key)]);

        // Act — the background scan completes; the callback is captured but NOT yet executed
        await viewManager.FullAggregationDrillDownAsync(request);

        // Assert — phase 1: state must be untouched before the UI-thread callback runs
        capturedCallbacks.Should().ContainSingle();
        state.DrillDown.Should().BeNull();
        state.CurrentMode.Should().Be(ViewMode.FileSelection);

        // Act — dispatch the captured callback (what the real UI thread would run)
        capturedCallbacks[0].Invoke();

        // Assert — phase 2: only now does the successful result reach AppState and switch the view
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        state.DrillDown.Should().NotBeNull();
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WhenViewManagerDisposedBeforeCallback_Throws()
    {
        // Arrange — a success scan is required to reach SwitchToFocusedTable's dispose guard;
        // dispose before invoking the captured callback to simulate the race.
        var filePath = CreateTempFile(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath };
        using var window = new Window();
        var modeController = new ModeController(state);

        List<Action> capturedCallbacks = [];
        // Not 'using' — disposed manually below to simulate the race between scan completion and dispatch.
        var viewManager = new ViewManager(
            window, state, modeController, cb => capturedCallbacks.Add(cb));

        var request = new FullAggregationDrillDownRequest(
            DataFormat.JsonLines,
            [new KeyPathSegment("user", KeyPathSegmentKind.Key)]);

        // Act — scan completes and the callback is captured; dispose BEFORE the callback executes
        await viewManager.FullAggregationDrillDownAsync(request);
        viewManager.Dispose();

        // Assert — the fail-fast guard throws before any state mutation.
        capturedCallbacks.Should().ContainSingle();
        var act = () => capturedCallbacks[0].Invoke();
        act.Should().Throw<ObjectDisposedException>();
        state.DrillDown.Should().BeNull();
        state.CurrentMode.Should().Be(ViewMode.FileSelection);
    }

    [Fact]
    public void SwitchToCsvTable_WithStaleKeyPath_ResetsCurrentKeyPath()
    {
        // Arrange — a leftover KeyPath from a prior Tree/FocusedTable session must not leak into CsvTable
        var filePath = CreateTempFile(".csv", "col1\nvalue1\n");
        using var app = CreateTestApp();
        using var state = new AppState
        {
            CurrentFilePath = filePath,
            CurrentKeyPath = [new KeyPathSegment("stale", KeyPathSegmentKind.Key)],
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
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
        using var app = CreateTestApp();
        using var state = new AppState
        {
            CurrentFilePath = filePath,
            CurrentKeyPath = [new KeyPathSegment("stale", KeyPathSegmentKind.Key)],
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
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
        using var app = CreateTestApp();
        using var state = new AppState
        {
            CurrentKeyPath = [new KeyPathSegment("stale", KeyPathSegmentKind.Key)],
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

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
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        var request = new SingleDrillDownRequest(
            DataFormat.JsonObject,
            Encoding.UTF8.GetBytes("[{\"a\":1}]"),
            [
                new KeyPathSegment("list", KeyPathSegmentKind.Key),
                new KeyPathSegment("[0]", KeyPathSegmentKind.Index),
            ]);

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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        var request = new FullAggregationDrillDownRequest(
            DataFormat.JsonLines,
            [
                new KeyPathSegment("list", KeyPathSegmentKind.Key),
                new KeyPathSegment("[0]", KeyPathSegmentKind.Index),
            ]);

        // Act
        await viewManager.FullAggregationDrillDownAsync(request);

        // Assert
        window.SubViews.OfType<BreadcrumbBar>().Single().Text.Should().Be("list[*]");
    }

    [Fact]
    public void SwitchToFocusedTable_WithEmptyActionStack_SetsMorphCallbacksOnRawSource()
    {
        // Arrange
        using var app = CreateTestApp();
        var schema = new TableSchema
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        DrillDownState drillDown = new(
            [new FocusedTableRow(Encoding.UTF8.GetBytes("{\"name\":\"Alice\"}"), "[0]")],
            schema,
            ViewMode.JsonArrayTree);
        using var state = new AppState { CurrentMode = ViewMode.FocusedTable, DrillDown = drillDown };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        // Act
        viewManager.SwitchToFocusedTable(drillDown);

        // Assert — callbacks are wired even with no actions, so Morph works from the first DrillDown
        var view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<FocusedTableSource>();
        view.OnMorphAction.Should().NotBeNull();
        view.GetRawColumnName.Should().NotBeNull();
        var rawName = view.GetRawColumnName?.Invoke(1);
        rawName.Should().Be("name");
    }

    [Fact]
    public void RefreshCurrentTableView_WithFocusedTableModeAndDrillDown_RendersThroughFocusedTableTransformer()
    {
        // Arrange — a pending action proves the re-render went through SwitchToFocusedTable's wrap path
        using var app = CreateTestApp();
        var schema = new TableSchema
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        DrillDownState drillDown = new(
            [new FocusedTableRow(Encoding.UTF8.GetBytes("{\"name\":\"Alice\"}"), "[0]")],
            schema,
            ViewMode.JsonArrayTree);
        using var state = new AppState { CurrentMode = ViewMode.FocusedTable, DrillDown = drillDown };
        state.AddMorphAction(new RenameColumnAction { OldName = "name", NewName = "label" });
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        // Act
        viewManager.RefreshCurrentTableView();

        // Assert
        var view = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        view.Table.Should().BeOfType<FocusedTableTransformer>();
        view.Table.ColumnNames.Should().Contain("label (text)");
    }

    [Fact]
    public void DrillDown_OnSuccess_ClearsActionStack()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        var request = new SingleDrillDownRequest(
            DataFormat.JsonObject,
            Encoding.UTF8.GetBytes("[{\"a\":1}]"),
            [new KeyPathSegment("list", KeyPathSegmentKind.Key)]);

        // Act
        viewManager.DrillDown(request);

        // Assert
        state.ActionStack.Should().BeEmpty();
    }

    [Fact]
    public void DrillDown_OnFailure_LeavesActionStackUntouched()
    {
        // Arrange — an empty array node fails schema extraction, routing to ShowError
        using var app = CreateTestApp();
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        var request = new SingleDrillDownRequest(
            DataFormat.JsonObject,
            Encoding.UTF8.GetBytes("[]"),
            [new KeyPathSegment("list", KeyPathSegmentKind.Key)]);

        // Act
        viewManager.DrillDown(request);

        // Assert — the failed DrillDown leaves the previous actions (and error view) in place
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        state.ActionStack.Should().ContainSingle();
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_OnSuccess_ClearsActionStack()
    {
        // Arrange
        var filePath = CreateTempFile(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath };
        state.AddMorphAction(new RenameColumnAction { OldName = "name", NewName = "label" });
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        var request = new FullAggregationDrillDownRequest(
            DataFormat.JsonLines,
            [new KeyPathSegment("user", KeyPathSegmentKind.Key)]);

        // Act
        await viewManager.FullAggregationDrillDownAsync(request);

        // Assert
        state.ActionStack.Should().BeEmpty();
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_OnFailure_LeavesActionStackUntouched()
    {
        // Arrange — a non-matching KeyPath forces the scan to fail
        var filePath = CreateTempFile(".jsonl", "{\"user\":{\"name\":\"Alice\"}}\n");
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath };
        state.AddMorphAction(new RenameColumnAction { OldName = "name", NewName = "label" });
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        var request = new FullAggregationDrillDownRequest(
            DataFormat.JsonLines,
            [new KeyPathSegment("missing", KeyPathSegmentKind.Key)]);

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
        using var app = CreateTestApp();
        var indexer = new Refedle.Engine.IO.JsonLines.RowIndexer(filePath);
        indexer.BuildIndex();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState
        {
            CurrentFilePath = filePath,
            CurrentMode = ViewMode.FocusedTable,
            RowIndexer = indexer,
            DrillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonLinesTree),
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        // Act
        viewManager.ReturnFromDrillDown();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
        state.DrillDown.Should().BeNull();
        viewManager.GetCurrentView().Should().BeOfType<JsonLinesTreeView>();
    }

    [Fact]
    public void ReturnFromDrillDown_WithJsonArrayTreePreviousMode_RestoresJsonArrayTree()
    {
        // Arrange
        var filePath = CreateTempFile(".json", "[1,2,3]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonArray, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState
        {
            CurrentFilePath = filePath,
            CurrentMode = ViewMode.FocusedTable,
            RowIndexer = indexer,
            DrillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonArrayTree),
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        // Act
        viewManager.ReturnFromDrillDown();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonArrayTree);
        state.DrillDown.Should().BeNull();
        viewManager.GetCurrentView().Should().BeOfType<JsonArrayTreeView>();
    }

    [Fact]
    public void ReturnFromDrillDown_WithJsonObjectTreePreviousMode_RestoresJsonObjectTree()
    {
        // Arrange
        using var app = CreateTestApp();
        IReadOnlyList<JsonObjectEntry> entries = [new JsonObjectEntry("id", Encoding.UTF8.GetBytes("1"))];
        var schema = new TableSchema { SourceFormat = DataFormat.JsonObject, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState
        {
            CurrentFilePath = "test.json",
            CurrentMode = ViewMode.FocusedTable,
            JsonObjectEntries = entries,
            DrillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonObjectTree),
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        // Act
        viewManager.ReturnFromDrillDown();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonObjectTree);
        state.DrillDown.Should().BeNull();
        viewManager.GetCurrentView().Should().BeOfType<JsonObjectTreeView>();
    }

    [Fact]
    public void ReturnFromDrillDown_WithNullDrillDown_DoesNothing()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentMode = ViewMode.FileSelection };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        viewManager.SwitchToFileSelection();

        // Act
        viewManager.ReturnFromDrillDown();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.FileSelection);
        state.DrillDown.Should().BeNull();
    }

    [Fact]
    public void ReturnFromDrillDown_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        var viewManager = new ViewManager(window, state, modeController, action => action());
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
}

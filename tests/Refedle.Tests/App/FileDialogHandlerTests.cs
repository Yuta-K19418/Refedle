using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Views;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace Refedle.Tests.App;

public sealed class FileDialogHandlerTests : IDisposable
{
    private readonly string _jsonLinesFile;
    private readonly string _jsonObjectFile;
    private readonly string _csvFile;
    private readonly string _jsonArrayFile;
    private readonly string _unsupportedFile;

    public FileDialogHandlerTests()
    {
        _jsonLinesFile = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
        File.WriteAllText(_jsonLinesFile, "{\"id\":1}\n");

        _jsonObjectFile = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(_jsonObjectFile, "{\"name\":\"test\",\"count\":42}");

        _csvFile = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        File.WriteAllText(_csvFile, "header\ndata");

        _jsonArrayFile = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(_jsonArrayFile, "[{\"id\":1}]");

        _unsupportedFile = Path.ChangeExtension(Path.GetTempFileName(), ".txt");
        File.WriteAllText(_unsupportedFile, "not a data file");
    }

    public void Dispose()
    {
        if (File.Exists(_jsonLinesFile))
        {
            File.Delete(_jsonLinesFile);
        }

        if (File.Exists(_jsonObjectFile))
        {
            File.Delete(_jsonObjectFile);
        }

        if (File.Exists(_csvFile))
        {
            File.Delete(_csvFile);
        }

        if (File.Exists(_jsonArrayFile))
        {
            File.Delete(_jsonArrayFile);
        }

        if (File.Exists(_unsupportedFile))
        {
            File.Delete(_unsupportedFile);
        }
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
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        // Act
        Action act = () =>
        {
            _ = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonLinesFile_SwitchesToTreeViewAfterFirstCheckpoint()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        IRowIndexer? capturedIndexer = null;
        var handler = new FileDialogHandler(app, state, viewManager, indexer =>
        {
            capturedIndexer = indexer;
            // Simulate indexing start
            Task.Run(() => indexer.BuildIndex());
        }, () => { });

        // Act
        app.Begin(window);
        await handler.HandleFileSelectedAsync(_jsonLinesFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
        viewManager.GetCurrentView().Should().BeOfType<JsonLinesTreeView>();
        Assert.NotNull(capturedIndexer);
        capturedIndexer.TotalRows.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonObjectFile_SwitchesToJsonObjectTree()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        var handler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });

        // Act
        app.Begin(window);
        await handler.HandleFileSelectedAsync(_jsonObjectFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonObjectTree);
        viewManager.GetCurrentView().Should().BeOfType<JsonObjectTreeView>();
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonObjectFile_WhenCancelled_DoesNotSwitchView()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        viewManager.SwitchToFileSelection();

        // _stopIndexing is called after RenewCtsWithCancel(), so cancelling state.Cts
        // here pre-cancels the token before TopLevelScanner.Scan runs.
        var handler = new FileDialogHandler(app, state, viewManager, _ => { },
            () => state.Cts.Cancel());

        // Act
        app.Begin(window);
        await handler.HandleFileSelectedAsync(_jsonObjectFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.CurrentMode.Should().NotBe(ViewMode.JsonObjectTree);
        viewManager.GetCurrentView().Should().NotBeOfType<JsonObjectTreeView>();
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonLinesFileBeforeFirstCheckpoint_DoesNotSwitchToTreeView()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        viewManager.SwitchToFileSelection(); // Ensure initial view is not null

        var tcs = new TaskCompletionSource();
        var handler = new FileDialogHandler(app, state, viewManager, _ =>
        {
            // Do NOT start indexing yet, so FirstCheckpointReached won't fire
            tcs.TrySetResult();
        }, () => { });

        // Act
        app.Begin(window);
        var handleTask = handler.HandleFileSelectedAsync(_jsonLinesFile);
        await tcs.Task; // Wait until _onIndexerStart is called

        // Process any potential early Invokes
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.CurrentMode.Should().NotBe(ViewMode.JsonLinesTree);
        viewManager.GetCurrentView().Should().NotBeOfType<JsonLinesTreeView>();

        // Cleanup: actually start indexing to let the task complete
        Assert.NotNull(state.RowIndexer);
        state.RowIndexer.BuildIndex();
        await handleTask;
    }

    [Fact]
    public async Task HandleFileSelectedAsync_WhenDrillDownStateIsPopulated_ResetsDrillDownState()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        var schema = new TableSchema
        {
            SourceFormat = DataFormat.JsonObject,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
        };
        state.DrillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            schema,
            ViewMode.JsonObjectTree,
            ActionStack: []);

        var handler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });

        // Act
        app.Begin(window);
        await handler.HandleFileSelectedAsync(_jsonObjectFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.DrillDown.Should().BeNull();
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonObjectFile_PopulatesJsonObjectEntries()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var handler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });

        // Act
        app.Begin(window);
        await handler.HandleFileSelectedAsync(_jsonObjectFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.JsonObjectEntries.Should().NotBeNull();
        state.JsonObjectEntries.Should().HaveCount(2);
        state.JsonObjectEntries.Should().SatisfyRespectively(
            e => e.Key.Should().Be("name"),
            e => e.Key.Should().Be("count"));
    }

    [Fact]
    public async Task HandleFileSelectedAsync_NonJsonObjectFile_ResetsJsonObjectEntriesToNull()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState
        {
            JsonObjectEntries = [new JsonObjectEntry("stale", JsonRawBytes.Empty)],
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        IRowIndexer? capturedIndexer = null;
        var handler = new FileDialogHandler(app, state, viewManager, indexer =>
        {
            capturedIndexer = indexer;
            Task.Run(() => indexer.BuildIndex());
        }, () => { });

        // Act
        app.Begin(window);
        await handler.HandleFileSelectedAsync(_jsonLinesFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.JsonObjectEntries.Should().BeNull();
        capturedIndexer.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleFileSelectedAsync_CsvFile_SwitchesToCsvTable()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        var handler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });

        // Act
        app.Begin(window);
        await handler.HandleFileSelectedAsync(_csvFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.CsvTable);
        viewManager.GetCurrentView().Should().BeOfType<CsvTableView>();
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonArrayFile_SwitchesToTreeViewAfterFirstCheckpoint()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        IRowIndexer? capturedIndexer = null;
        var handler = new FileDialogHandler(app, state, viewManager, indexer =>
        {
            capturedIndexer = indexer;
            // Simulate indexing start
            Task.Run(() => indexer.BuildIndex());
        }, () => { });

        // Act
        app.Begin(window);
        await handler.HandleFileSelectedAsync(_jsonArrayFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonArrayTree);
        viewManager.GetCurrentView().Should().BeOfType<JsonArrayTreeView>();
        Assert.NotNull(capturedIndexer);
        capturedIndexer.TotalRows.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HandleFileSelectedAsync_UnsupportedExtension_ShowsErrorAndSwitchesToPlaceholderView()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        var handler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });

        // Act
        app.Begin(window);
        await handler.HandleFileSelectedAsync(_unsupportedFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        viewManager.GetCurrentView().Should().BeOfType<PlaceholderView>()
            .Which.Text.Should().Contain("Unsupported file format: .txt");
    }
}

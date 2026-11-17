using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.IntegrationTests.App.Tui.Workers.Schema;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.IntegrationTests.App.Tui.UI;

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

    /// <summary>
    /// Waits for the next <see cref="OpenDialog"/> to become the active runnable, then sets its
    /// <see cref="FileDialog.Path"/> and invokes <see cref="Command.Accept"/> on it — the same
    /// command a real "OK"/Enter acceptance raises — so tests can drive the real modal dialog
    /// opened by <see cref="FileDialogHandler.ShowAsync"/> instead of calling past it.
    /// </summary>
    private static void AcceptFirstOpenDialog(IApplication app, string path)
    {
        EventHandler<EventArgs<IApplication?>>? handler = null;
        handler = (_, _) =>
        {
            if (app.TopRunnableView is OpenDialog dialog)
            {
                dialog.Path = path;
                dialog.InvokeCommand(Command.Accept);
                app.Iteration -= handler;
            }
        };
        app.Iteration += handler;
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonLinesFile_SwitchesToTreeViewAfterFirstCheckpoint()
    {
        // Arrange
        IRowIndexer? capturedIndexer = null;
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            FileDialogHandler handler = new(app, state, viewManager, indexer =>
            {
                capturedIndexer = indexer;
                // Simulate indexing start
                Task.Run(() => indexer.BuildIndex());
            }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.HandleFileSelectedAsync(_jsonLinesFile));
        (ViewMode mode, Type? viewType) = await session.InvokeAsync((_, ctx) =>
            Task.FromResult((ctx.State.CurrentMode, ctx.ViewManager.GetCurrentView()?.GetType())));

        // Assert
        mode.Should().Be(ViewMode.JsonLinesTree);
        viewType.Should().Be<JsonLinesTreeView>();
        Assert.NotNull(capturedIndexer);
        capturedIndexer.TotalRows.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonObjectFile_SwitchesToJsonObjectTree()
    {
        // Arrange
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            FileDialogHandler handler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.HandleFileSelectedAsync(_jsonObjectFile));
        (ViewMode mode, Type? viewType) = await session.InvokeAsync((_, ctx) =>
            Task.FromResult((ctx.State.CurrentMode, ctx.ViewManager.GetCurrentView()?.GetType())));

        // Assert
        mode.Should().Be(ViewMode.JsonObjectTree);
        viewType.Should().Be<JsonObjectTreeView>();
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonObjectFile_WhenCancelled_DoesNotSwitchView()
    {
        // Arrange
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            viewManager.SwitchToFileSelection();

            // _stopIndexing is called after RenewCtsWithCancel(), so cancelling state.Cts
            // here pre-cancels the token before TopLevelScanner.Scan runs.
            FileDialogHandler handler = new(app, state, viewManager, _ => { },
                () => state.Cts.Cancel(), TestSchemaScannerFactories.Csv, _ => { });
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.HandleFileSelectedAsync(_jsonObjectFile));
        (ViewMode mode, Type? viewType) = await session.InvokeAsync((_, ctx) =>
            Task.FromResult((ctx.State.CurrentMode, ctx.ViewManager.GetCurrentView()?.GetType())));

        // Assert
        mode.Should().NotBe(ViewMode.JsonObjectTree);
        viewType.Should().NotBe<JsonObjectTreeView>();
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonLinesFileBeforeFirstCheckpoint_DoesNotSwitchToTreeView()
    {
        // Arrange
        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            viewManager.SwitchToFileSelection(); // Ensure initial view is not null

            FileDialogHandler handler = new(app, state, viewManager, _ =>
            {
                // Do NOT start indexing yet, so FirstCheckpointReached won't fire
                tcs.TrySetResult();
            }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act — the pump stays live for the whole scenario: start the handler, wait for
        // _onIndexerStart's completion, then read the pre-checkpoint state on the loop thread.
        Task handleTask = await session.InvokeAsync(async (_, ctx) =>
        {
            Task task = ctx.Handler.HandleFileSelectedAsync(_jsonLinesFile);
            await tcs.Task; // Wait until _onIndexerStart is called
            return task;
        });
        (ViewMode modeBeforeCheckpoint, Type? viewTypeBeforeCheckpoint) = await session.InvokeAsync((_, ctx) =>
            Task.FromResult((ctx.State.CurrentMode, ctx.ViewManager.GetCurrentView()?.GetType())));

        // Assert
        handleTask.IsCompleted.Should().BeFalse();
        modeBeforeCheckpoint.Should().NotBe(ViewMode.JsonLinesTree);
        viewTypeBeforeCheckpoint.Should().NotBe<JsonLinesTreeView>();

        // Cleanup: actually start indexing to let the task complete, on the same live pump.
        await session.InvokeAsync((_, ctx) =>
        {
            Assert.NotNull(ctx.State.RowIndexer);
            ctx.State.RowIndexer.BuildIndex();
            return handleTask;
        });
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonLinesFile_NotifiesFilePathChangedWithSelectedPath()
    {
        // Arrange
        List<string> notifiedPaths = [];
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            FileDialogHandler handler = new(
                app,
                state,
                viewManager,
                indexer => Task.Run(() => indexer.BuildIndex()),
                () => { },
                TestSchemaScannerFactories.JsonLines,
                notifiedPaths.Add);
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.HandleFileSelectedAsync(_jsonLinesFile));
        string[] paths = await session.InvokeAsync((_, _) => Task.FromResult(notifiedPaths.ToArray()));

        // Assert
        paths.Should().Equal(_jsonLinesFile);
    }

    [Fact]
    public async Task HandleFileSelectedAsync_UnsupportedFile_DoesNotNotifyFilePathChanged()
    {
        // Arrange
        List<string> notifiedPaths = [];
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            FileDialogHandler handler = new(
                app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.JsonLines, notifiedPaths.Add);
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.HandleFileSelectedAsync(_unsupportedFile));
        string[] paths = await session.InvokeAsync((_, _) => Task.FromResult(notifiedPaths.ToArray()));

        // Assert
        paths.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleFileSelectedAsync_WhenDrillDownStateIsPopulated_ResetsDrillDownState()
    {
        // Arrange
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);

            TableSchema schema = new()
            {
                SourceFormat = DataFormat.JsonObject,
                Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
            };
            DrillDownState drillDown = new(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonObjectTree,
                KeyPath: [],
                ActionStack: []);
            state.EnterFocusedTable(drillDown);

            FileDialogHandler handler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.HandleFileSelectedAsync(_jsonObjectFile));
        DrillDownState? drillDown = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.State.GetDrillDownOrNull()));

        // Assert
        drillDown.Should().BeNull();
    }

    [Fact]
    public async Task HandleFileSelectedAsync_WhenSessionWasDirty_StartsNewSessionClean()
    {
        // Arrange — opening a new file discards the previous session's stack; the fresh session
        // must not inherit its unsaved-changes flag
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            FileDialogHandler handler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.HandleFileSelectedAsync(_jsonObjectFile));
        (bool hasUnsavedChanges, int actionStack) = await session.InvokeAsync((_, ctx) =>
            Task.FromResult((ctx.State.HasUnsavedChanges, ctx.State.ActionStack.Count)));

        // Assert
        hasUnsavedChanges.Should().BeFalse();
        actionStack.Should().Be(0);
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonObjectFile_PopulatesJsonObjectEntries()
    {
        // Arrange
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            FileDialogHandler handler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.HandleFileSelectedAsync(_jsonObjectFile));
        string[]? entryKeys = await session.InvokeAsync((_, ctx) =>
            Task.FromResult(ctx.State.JsonObjectEntries?.Select(e => e.Key).ToArray()));

        // Assert
        entryKeys.Should().Equal("name", "count");
    }

    [Fact]
    public async Task HandleFileSelectedAsync_NonJsonObjectFile_ResetsJsonObjectEntriesToNull()
    {
        // Arrange
        IRowIndexer? capturedIndexer = null;
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            state.EnterJsonObjectTree([new JsonObjectEntry("stale", JsonRawBytes.Empty)]);
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            FileDialogHandler handler = new(app, state, viewManager, indexer =>
            {
                capturedIndexer = indexer;
                Task.Run(() => indexer.BuildIndex());
            }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.HandleFileSelectedAsync(_jsonLinesFile));
        IReadOnlyList<JsonObjectEntry>? entries = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.State.JsonObjectEntries));

        // Assert
        entries.Should().BeNull();
        capturedIndexer.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleFileSelectedAsync_CsvFile_SwitchesToCsvTable()
    {
        // Arrange
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            FileDialogHandler handler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.HandleFileSelectedAsync(_csvFile));
        (ViewMode mode, Type? viewType) = await session.InvokeAsync((_, ctx) =>
            Task.FromResult((ctx.State.CurrentMode, ctx.ViewManager.GetCurrentView()?.GetType())));

        // Assert
        mode.Should().Be(ViewMode.CsvTable);
        viewType.Should().Be<CsvTableView>();
    }

    [Fact]
    public async Task HandleFileSelectedAsync_CsvFile_StartsBackgroundScanOnlyAfterIndexingCompletes()
    {
        // Arrange
        GatedSchemaScanner scanner = new(CreateCsvSchema());
        scanner.Release();
        TaskCompletionSource<IRowIndexer> indexerStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            FileDialogHandler handler = new(
                app, state, viewManager, indexer => indexerStarted.TrySetResult(indexer), () => { }, _ => scanner, _ => { });
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.HandleFileSelectedAsync(_csvFile));
        IRowIndexer indexer = await indexerStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        bool startedBeforeIndexing = scanner.BackgroundScanStarted.IsCompleted;
        indexer.BuildIndex();
        await scanner.BackgroundScanStarted.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        startedBeforeIndexing.Should().BeFalse();
    }

    private static TableSchema CreateCsvSchema() => new()
    {
        Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }],
        SourceFormat = DataFormat.Csv
    };

    [Fact]
    public async Task HandleFileSelectedAsync_CsvSessionReplacedDuringInitialScan_KeepsReplacementFileState()
    {
        // Arrange
        string csvPath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        try
        {
            await File.WriteAllTextAsync(csvPath, "col1\ndata1");
            GatedSchemaScanner scanner = new(CreateCsvSchema());
            TableSchema? callbackSchema = null;
            int indexerStartCount = 0;
            await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
            {
                AppState state = new();
                state.SetSchemaRefinedCallback(schema => callbackSchema = schema);
                ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
                ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
                FileDialogHandler handler = new(
                    app, state, viewManager, _ => Interlocked.Increment(ref indexerStartCount), () => { }, _ => scanner, _ => { });
                return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
            });
            TableSchema replacementSchema = new()
            {
                Columns = [new ColumnSchema { Name = "id", Type = ColumnType.WholeNumber }],
                SourceFormat = DataFormat.Csv
            };

            // Act — start the load on the UI loop without awaiting it, then replace the
            // session on the UI loop while the gated initial scan is still pending.
            Task loadTask = Task.CompletedTask;
            await session.InvokeAsync((_, ctx) =>
            {
                loadTask = ctx.Handler.HandleFileSelectedAsync(csvPath);
                return Task.CompletedTask;
            });
            await scanner.Started.WaitAsync(TimeSpan.FromSeconds(10));
            await session.InvokeAsync((_, ctx) =>
            {
                ctx.State.RenewCtsWithCancel();
                ctx.State.ApplyRefinedSchema(replacementSchema);
                return Task.CompletedTask;
            });
            scanner.Release();
            await loadTask;
            (ViewMode mode, Type? viewType, TableSchema? schema) = await session.InvokeAsync((_, ctx) =>
                Task.FromResult((ctx.State.CurrentMode, ctx.ViewManager.GetCurrentView()?.GetType(), ctx.State.Schema)));

            // Assert
            mode.Should().Be(ViewMode.FileSelection);
            viewType.Should().BeNull();
            schema.Should().BeSameAs(replacementSchema);
            callbackSchema.Should().BeNull();
            Volatile.Read(ref indexerStartCount).Should().Be(0);
        }
        finally
        {
            File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task HandleFileSelectedAsync_CsvScanFailsAfterSessionReplacement_ShowsNoStaleError()
    {
        // Arrange
        string csvPath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        try
        {
            await File.WriteAllTextAsync(csvPath, "col1\ndata1");
            GatedSchemaScanner scanner = new(CreateCsvSchema(), new InvalidOperationException("scan failed"));
            TableSchema? callbackSchema = null;
            int indexerStartCount = 0;
            await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
            {
                AppState state = new();
                state.SetSchemaRefinedCallback(schema => callbackSchema = schema);
                ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
                ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
                FileDialogHandler handler = new(
                    app, state, viewManager, _ => Interlocked.Increment(ref indexerStartCount), () => { }, _ => scanner, _ => { });
                return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
            });
            TableSchema replacementSchema = new()
            {
                Columns = [new ColumnSchema { Name = "id", Type = ColumnType.WholeNumber }],
                SourceFormat = DataFormat.Csv
            };

            // Act — start the load on the UI loop, replace the session on the
            // UI loop, then release the configured failing scan.
            Task loadTask = Task.CompletedTask;
            await session.InvokeAsync((_, ctx) =>
            {
                loadTask = ctx.Handler.HandleFileSelectedAsync(csvPath);
                return Task.CompletedTask;
            });
            await scanner.Started.WaitAsync(TimeSpan.FromSeconds(10));
            await session.InvokeAsync((_, ctx) =>
            {
                ctx.State.RenewCtsWithCancel();
                ctx.State.ApplyRefinedSchema(replacementSchema);
                return Task.CompletedTask;
            });
            scanner.Release();
            await loadTask;
            (ViewMode mode, Type? viewType, TableSchema? schema) = await session.InvokeAsync((_, ctx) =>
                Task.FromResult((ctx.State.CurrentMode, ctx.ViewManager.GetCurrentView()?.GetType(), ctx.State.Schema)));

            // Assert — no stale error view is shown for the replaced session.
            mode.Should().Be(ViewMode.FileSelection);
            viewType.Should().BeNull();
            schema.Should().BeSameAs(replacementSchema);
            callbackSchema.Should().BeNull();
            Volatile.Read(ref indexerStartCount).Should().Be(0);
        }
        finally
        {
            File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task HandleFileSelectedAsync_JsonArrayFile_SwitchesToTreeViewAfterFirstCheckpoint()
    {
        // Arrange
        IRowIndexer? capturedIndexer = null;
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            FileDialogHandler handler = new(app, state, viewManager, indexer =>
            {
                capturedIndexer = indexer;
                // Simulate indexing start
                Task.Run(() => indexer.BuildIndex());
            }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.HandleFileSelectedAsync(_jsonArrayFile));
        (ViewMode mode, Type? viewType) = await session.InvokeAsync((_, ctx) =>
            Task.FromResult((ctx.State.CurrentMode, ctx.ViewManager.GetCurrentView()?.GetType())));

        // Assert
        mode.Should().Be(ViewMode.JsonArrayTree);
        viewType.Should().Be<JsonArrayTreeView>();
        Assert.NotNull(capturedIndexer);
        capturedIndexer.TotalRows.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HandleFileSelectedAsync_UnsupportedExtension_ShowsErrorAndSwitchesToPlaceholderView()
    {
        // Arrange
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            FileDialogHandler handler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.HandleFileSelectedAsync(_unsupportedFile));
        (ViewMode mode, bool isPlaceholder, string? placeholderText) = await session.InvokeAsync((_, ctx) =>
        {
            View? view = ctx.ViewManager.GetCurrentView();
            string? text = view is PlaceholderView placeholderView ? placeholderView.Text : null;
            return Task.FromResult((ctx.State.CurrentMode, view is PlaceholderView, text));
        });

        // Assert
        mode.Should().Be(ViewMode.PlaceholderView);
        isPlaceholder.Should().BeTrue();
        placeholderText.Should().Contain("Unsupported file format: .txt");
    }

    [Fact]
    public async Task ShowAsync_WithAcceptedOpenDialog_SwitchesToJsonLinesTreeView()
    {
        // Arrange
        await using LivePumpTestSession<LiveTestContext<FileDialogHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            FileDialogHandler handler = new(app, state, viewManager, indexer =>
            {
                Task.Run(() => indexer.BuildIndex());
            }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
            return new LiveTestContext<FileDialogHandler>(state, viewManager, handler);
        });

        // Act — drive the real OpenDialog to acceptance, exercising the ShowAsync continuation
        // that reads dialog.Canceled/dialog.Path (and calls into HandleFileSelectedAsync) after
        // IApplication.RunAsync returns.
        await session.InvokeAsync((app, ctx) =>
        {
            AcceptFirstOpenDialog(app, _jsonLinesFile);
            return ctx.Handler.ShowAsync();
        });
        (ViewMode mode, Type? viewType) = await session.InvokeAsync((_, ctx) =>
            Task.FromResult((ctx.State.CurrentMode, ctx.ViewManager.GetCurrentView()?.GetType())));

        // Assert
        mode.Should().Be(ViewMode.JsonLinesTree);
        viewType.Should().Be<JsonLinesTreeView>();
    }
}

using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Views;
using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Recipes;
using Refedle.Engine.Types;
using Refedle.Tests.App.Schema;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.Tests.App;

public sealed partial class RecipeCommandHandlerTests
{
    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        Assert.NotNull(app.Driver);
        app.Driver.SetScreenSize(80, 25);
        return app;
    }

    /// <summary>
    /// On every loop iteration, accepts whichever modal is currently on top of <paramref name="mainWindow"/>:
    /// a <see cref="FileDialog"/> gets <paramref name="path"/> set before <see cref="Command.Accept"/>,
    /// any other modal (e.g. the success/error <c>MessageBox</c> that follows Save/Load) just gets
    /// <see cref="Command.Accept"/> — the same command a real "OK"/Enter acceptance raises. This drives
    /// the real modal sequence opened by <see cref="RecipeCommandHandler.SaveAsync"/>/
    /// <see cref="RecipeCommandHandler.LoadAsync"/> instead of calling past it, so any dialog left
    /// unattended (which would otherwise hang the nested loop for the rest of the test) is closed.
    /// </summary>
    private static void AcceptModalDialogs(IApplication app, View mainWindow, string path)
    {
        app.Iteration += (_, _) =>
        {
            if (app.TopRunnableView is FileDialog fileDialog)
            {
                fileDialog.Path = path;
                fileDialog.InvokeCommand(Command.Accept);
                return;
            }

            if (app.TopRunnableView is { } topView && !ReferenceEquals(topView, mainWindow))
            {
                topView.InvokeCommand(Command.Accept);
            }
        };
    }

    [Fact]
    public async Task SaveAsync_WithNonTableMode_DoesNothing()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var handler = new RecipeCommandHandler(app, state, viewManager);

        // Act
        Func<Task> act = async () => await handler.SaveAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void BuildRecipe_FromFocusedTableWithActiveDrillDown_UsesDrillDownKeyPathAndActionStack()
    {
        // Arrange
        using var app = CreateTestApp();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("orders", KeyPathSegmentKind.Key)];
        var drillDownAction = new RenameColumnAction { OldName = "drill", NewName = "renamed_drill" };
        using var state = new AppState();
        state.StartNewFile("data.jsonl");
        var drillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            schema,
            ViewMode.JsonLinesTree,
            keyPath,
            ActionStack: [drillDownAction]);
        state.EnterFocusedTable(drillDown);
        state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var handler = new RecipeCommandHandler(app, state, viewManager);

        // Act
        var recipe = handler.BuildRecipe();

        // Assert
        recipe.DrillDownKeyPath.Should().Equal(keyPath);
        recipe.Actions.Should().Equal(drillDownAction);
    }

    [Fact]
    public void BuildRecipe_FromTableModeWithStaleDrillDown_UsesBaseActionStackAndOmitsDrillDownKeyPath()
    {
        // Arrange — a stale DrillDown (an error view replaced FocusedTable without clearing it)
        // must be ignored when the current view is the base table, not FocusedTable
        using var app = CreateTestApp();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        var baseAction = new RenameColumnAction { OldName = "base", NewName = "renamed_base" };
        using var state = new AppState();
        state.StartNewFile("data.jsonl");
        var staleDrillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            schema,
            ViewMode.JsonLinesTree,
            KeyPath: [new KeyPathSegment("stale", KeyPathSegmentKind.Key)],
            ActionStack: [new RenameColumnAction { OldName = "stale", NewName = "stale_renamed" }]);
        state.EnterFocusedTable(staleDrillDown);
        state.EnterPlaceholderMode();
        state.AddMorphAction(baseAction);
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var handler = new RecipeCommandHandler(app, state, viewManager);

        // Act
        var recipe = handler.BuildRecipe();

        // Assert
        recipe.DrillDownKeyPath.Should().BeNull();
        recipe.Actions.Should().Equal(baseAction);
    }

    [Fact]
    public async Task LoadAsync_WithNoFilePath_DoesNothing()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var handler = new RecipeCommandHandler(app, state, viewManager);

        // Act
        Func<Task> act = async () => await handler.LoadAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveAsync_WithAcceptedDialog_SavesRecipeBuiltFromAppStateToSelectedPath()
    {
        // Arrange
        var action = new RenameColumnAction { OldName = "old", NewName = "new" };
        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, _recipeFile);

            var indexer = RowIndexerFactory.Create(DataFormat.Csv, _csvFile);
            var csvSchema = CreateCsvSchema();
            var state = new AppState();
            state.StartNewFile(_csvFile);
            state.CompleteCsvLoad(indexer, csvSchema);
            state.AddMorphAction(action);
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act — drive the real Save dialog (and the success MessageBox that follows) to acceptance,
        // exercising the SaveAsync continuation that reads dialog.Canceled/dialog.Path and builds
        // the recipe from AppState afterward.
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());

        // Assert
        var loadResult = await new RecipeManager().LoadAsync(_recipeFile);
        loadResult.IsSuccess.Should().BeTrue();
        loadResult.Value.Actions.Should().Equal(action);
    }

    [Fact]
    public async Task SaveAsync_WithAcceptedDialog_ClearsRootUnsavedChangesFlag()
    {
        // Arrange — the issue #340 scenario: actions applied (dirty), then saved successfully
        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, _recipeFile);

            var indexer = RowIndexerFactory.Create(DataFormat.Csv, _csvFile);
            var csvSchema = CreateCsvSchema();
            var state = new AppState();
            state.StartNewFile(_csvFile);
            state.CompleteCsvLoad(indexer, csvSchema);
            state.AddMorphAction(new RenameColumnAction { OldName = "old", NewName = "new" });
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());
        var hasUnsavedChanges = await session.InvokeAsync(
            (_, ctx) => Task.FromResult(ctx.State.HasUnsavedChanges));

        // Assert
        hasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_FromFocusedTableWithDirtyDrillDown_ClearsOnlyDrillDownFlag()
    {
        // Arrange — a FocusedTable save covers the DrillDown scope, so the DrillDown flag is
        // cleared while the dirty root stack (not part of the saved recipe) stays flagged
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, _recipeFile);

            var state = new AppState();
            state.StartNewFile(_jsonLinesFile);
            var drillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                KeyPath: [],
                ActionStack: [new RenameColumnAction { OldName = "drill", NewName = "renamed_drill" }],
                HasUnsavedChanges: true);
            state.EnterFocusedTable(drillDown);
            state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());
        var (drillDownFlag, rootFlag) = await session.InvokeAsync(
            (_, ctx) => Task.FromResult((ctx.State.GetDrillDownOrNull()?.HasUnsavedChanges, ctx.State.HasUnsavedChanges)));

        // Assert
        drillDownFlag.Should().BeFalse();
        rootFlag.Should().BeTrue();
    }

    /// <summary>
    /// A recipe manager whose save blocks until released, letting a test edit AppState while the
    /// write is in flight.
    /// </summary>
    private sealed class GatedRecipeManager : IRecipeManager
    {
        private readonly TaskCompletionSource _writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WriteStarted => _writeStarted.Task;

        public void Release() => _release.SetResult();

        public ValueTask<Result<Recipe>> LoadAsync(string filePath, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async ValueTask<Result> SaveAsync(Recipe recipe, string filePath, CancellationToken ct = default)
        {
            _writeStarted.SetResult();
            await _release.Task.ConfigureAwait(false);
            return Results.Success();
        }
    }

    private static Task<LivePumpTestSession<LiveTestContext<RecipeCommandHandler>>> StartGatedSaveSessionAsync(
        string csvFile, string recipeFile, GatedRecipeManager gatedManager) =>
        LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, recipeFile);

            var indexer = RowIndexerFactory.Create(DataFormat.Csv, csvFile);
            var csvSchema = CreateCsvSchema();
            var state = new AppState();
            state.StartNewFile(csvFile);
            state.CompleteCsvLoad(indexer, csvSchema);
            state.AddMorphAction(new RenameColumnAction { OldName = "old", NewName = "new" });
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager, gatedManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

    [Fact]
    public async Task SaveAsync_WhenRootActionAddedDuringWrite_KeepsUnsavedChangesFlag()
    {
        // Arrange — the write is suspended after the recipe was built; an edit lands before it completes
        var gatedManager = new GatedRecipeManager();
        await using var session = await StartGatedSaveSessionAsync(_csvFile, _recipeFile, gatedManager);
        var saveTask = session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());
        await gatedManager.WriteStarted;

        // Act
        await session.InvokeAsync((_, ctx) =>
        {
            ctx.State.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
            return Task.CompletedTask;
        });
        gatedManager.Release();
        await saveTask;
        var hasUnsavedChanges = await session.InvokeAsync(
            (_, ctx) => Task.FromResult(ctx.State.HasUnsavedChanges));

        // Assert
        hasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WhenRootStackClearedDuringWrite_KeepsUnsavedChangesFlag()
    {
        // Arrange — the saved (non-empty) stack is cleared while the write is in flight
        var gatedManager = new GatedRecipeManager();
        await using var session = await StartGatedSaveSessionAsync(_csvFile, _recipeFile, gatedManager);
        var saveTask = session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());
        await gatedManager.WriteStarted;

        // Act
        await session.InvokeAsync((_, ctx) =>
        {
            ctx.State.ClearMorphActions();
            return Task.CompletedTask;
        });
        gatedManager.Release();
        await saveTask;
        var hasUnsavedChanges = await session.InvokeAsync(
            (_, ctx) => Task.FromResult(ctx.State.HasUnsavedChanges));

        // Assert
        hasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ThenRootEdit_MarksRootUnsavedAgain()
    {
        // Arrange — dirty root, saved successfully (clean)
        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, _recipeFile);

            var indexer = RowIndexerFactory.Create(DataFormat.Csv, _csvFile);
            var csvSchema = CreateCsvSchema();
            var state = new AppState();
            state.StartNewFile(_csvFile);
            state.CompleteCsvLoad(indexer, csvSchema);
            state.AddMorphAction(new RenameColumnAction { OldName = "old", NewName = "new" });
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());

        // Act
        var (flagAfterSave, flagAfterEdit) = await session.InvokeAsync((_, ctx) =>
        {
            var afterSave = ctx.State.HasUnsavedChanges;
            ctx.State.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
            return Task.FromResult((afterSave, ctx.State.HasUnsavedChanges));
        });

        // Assert
        flagAfterSave.Should().BeFalse();
        flagAfterEdit.Should().BeTrue();
    }

    private static DrillDownState CreateDirtyDrillDown() =>
        new(
            [new FocusedTableRow(System.Text.Encoding.UTF8.GetBytes("{\"col1\":\"v\"}"), "[0]")],
            new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] },
            ViewMode.JsonLinesTree,
            KeyPath: [],
            ActionStack: [new RenameColumnAction { OldName = "drill", NewName = "renamed_drill" }],
            HasUnsavedChanges: true);

    private static TableSchema CreateCsvSchema() => new()
    {
        Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }],
        SourceFormat = DataFormat.Csv
    };

    [Fact]
    public async Task SaveAsync_FromFocusedTable_ThenDrillDownEdit_MarksDrillDownUnsavedAgain()
    {
        // Arrange — dirty DrillDown session, saved successfully (clean)
        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, _recipeFile);

            var drillDown = CreateDirtyDrillDown();
            var state = new AppState();
            state.StartNewFile(_jsonLinesFile);
            state.EnterFocusedTable(drillDown);
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            viewManager.SwitchToFocusedTable(drillDown);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());

        // Act
        var (flagAfterSave, flagAfterEdit) = await session.InvokeAsync((_, ctx) =>
        {
            var afterSave = ctx.State.GetDrillDownOrNull()?.HasUnsavedChanges;
            var view = ctx.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
            view.OnMorphAction?.Invoke(new RenameColumnAction { OldName = "col1", NewName = "again" });
            return Task.FromResult((afterSave, ctx.State.GetDrillDownOrNull()?.HasUnsavedChanges));
        });

        // Assert
        flagAfterSave.Should().BeFalse();
        flagAfterEdit.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_FromFocusedTableWithFailedWrite_KeepsDrillDownUnsavedChangesFlag()
    {
        // Arrange — the dialog path targets a missing directory, so the DrillDown save fails
        var unreachableRecipeFile = Path.Combine(
            Path.GetTempPath(), $"refedle-missing-{Guid.NewGuid():N}", "recipe.yaml");
        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, unreachableRecipeFile);

            var drillDown = CreateDirtyDrillDown();
            var state = new AppState();
            state.StartNewFile(_jsonLinesFile);
            state.EnterFocusedTable(drillDown);
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());
        var drillDownFlag = await session.InvokeAsync(
            (_, ctx) => Task.FromResult(ctx.State.GetDrillDownOrNull()?.HasUnsavedChanges));

        // Assert
        drillDownFlag.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WithFailedWrite_KeepsRootUnsavedChangesFlag()
    {
        // Arrange — the dialog path targets a missing directory, so RecipeManager.SaveAsync fails
        // and the unsaved flag must survive the error path
        var unreachableRecipeFile = Path.Combine(
            Path.GetTempPath(), $"refedle-missing-{Guid.NewGuid():N}", "recipe.yaml");
        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, unreachableRecipeFile);

            var indexer = RowIndexerFactory.Create(DataFormat.Csv, _csvFile);
            var csvSchema = CreateCsvSchema();
            var state = new AppState();
            state.StartNewFile(_csvFile);
            state.CompleteCsvLoad(indexer, csvSchema);
            state.AddMorphAction(new RenameColumnAction { OldName = "old", NewName = "new" });
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());
        var (hasUnsavedChanges, isPlaceholder) = await session.InvokeAsync(
            (_, ctx) => Task.FromResult((ctx.State.HasUnsavedChanges, ctx.ViewManager.GetCurrentView() is PlaceholderView)));

        // Assert — the write failed (error placeholder shown), so the flag must stay set
        isPlaceholder.Should().BeTrue();
        hasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_WithAcceptedDialog_LoadsRecipeFromSelectedPath()
    {
        // Arrange
        var action = new RenameColumnAction { OldName = "old", NewName = "new" };
        await SaveRecipeAsync(new Recipe { Name = "test", Actions = [action] }, _recipeFile);

        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, _recipeFile);

            var state = new AppState();
            state.StartNewFile(_jsonLinesFile);
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act — drive the real Load dialog to acceptance, exercising the LoadAsync continuation
        // that reads dialog.Canceled/dialog.Path before delegating to LoadFromPathAsync.
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadAsync());
        var actionStack = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.State.ActionStack.ToArray()));

        // Assert
        actionStack.Should().Equal(action);
    }
}

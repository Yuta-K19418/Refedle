using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Recipes;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui;
using Refedle.TestHelpers.App.Tui.Workers.Schema;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using static Refedle.IntegrationTests.App.Tui.UI.TestSchemas;

namespace Refedle.IntegrationTests.App.Tui.UI;

public sealed partial class RecipeCommandHandlerTests
{
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
    public async Task SaveAsync_WithAcceptedDialog_SavesRecipeBuiltFromAppStateToSelectedPath()
    {
        // Arrange
        RenameColumnAction action = new() { OldName = "old", NewName = "new" };
        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, _recipeFile);

            IRowIndexer indexer = RowIndexerFactory.Create(DataFormat.Csv, _csvFile);
            TableSchema csvSchema = CreateCsvTextColumnSchema();
            AppState state = new();
            state.StartNewFile(_csvFile);
            state.CompleteCsvLoad(indexer, csvSchema);
            state.AddMorphAction(action);
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act — drive the real Save dialog (and the success MessageBox that follows) to acceptance,
        // exercising the SaveAsync continuation that reads dialog.Canceled/dialog.Path and builds
        // the recipe from AppState afterward.
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());

        // Assert
        Result<Recipe> loadResult = await new RecipeManager().LoadAsync(_recipeFile);
        loadResult.IsSuccess.Should().BeTrue();
        loadResult.Value.Actions.Should().Equal(action);
    }

    [Fact]
    public async Task SaveAsync_WithAcceptedDialog_ClearsRootUnsavedChangesFlag()
    {
        // Arrange — actions applied (dirty), then saved successfully
        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, _recipeFile);

            IRowIndexer indexer = RowIndexerFactory.Create(DataFormat.Csv, _csvFile);
            TableSchema csvSchema = CreateCsvTextColumnSchema();
            AppState state = new();
            state.StartNewFile(_csvFile);
            state.CompleteCsvLoad(indexer, csvSchema);
            state.AddMorphAction(new RenameColumnAction { OldName = "old", NewName = "new" });
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());
        bool hasUnsavedChanges = await session.InvokeAsync(
            (_, ctx) => Task.FromResult(ctx.State.HasUnsavedChanges));

        // Assert
        hasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_FromFocusedTableWithDirtyDrillDown_ClearsOnlyDrillDownFlag()
    {
        // Arrange — a FocusedTable save covers the DrillDown scope, so the DrillDown flag is
        // cleared while the dirty root stack (not part of the saved recipe) stays flagged
        TableSchema schema = new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, _recipeFile);

            AppState state = new();
            state.StartNewFile(_jsonLinesFile);
            DrillDownState drillDown = new(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                KeyPath: [],
                ActionStack: [new RenameColumnAction { OldName = "drill", NewName = "renamed_drill" }],
                HasUnsavedChanges: true);
            state.EnterFocusedTable(drillDown);
            state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());
        (bool? drillDownFlag, bool rootFlag) = await session.InvokeAsync(
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

            IRowIndexer indexer = RowIndexerFactory.Create(DataFormat.Csv, csvFile);
            TableSchema csvSchema = CreateCsvTextColumnSchema();
            AppState state = new();
            state.StartNewFile(csvFile);
            state.CompleteCsvLoad(indexer, csvSchema);
            state.AddMorphAction(new RenameColumnAction { OldName = "old", NewName = "new" });
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager, gatedManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

    [Fact]
    public async Task SaveAsync_WhenRootActionAddedDuringWrite_KeepsUnsavedChangesFlag()
    {
        // Arrange — the write is suspended after the recipe was built; an edit lands before it completes
        GatedRecipeManager gatedManager = new();
        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await StartGatedSaveSessionAsync(_csvFile, _recipeFile, gatedManager);
        Task saveTask = session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());
        await gatedManager.WriteStarted;

        // Act
        await session.InvokeAsync((_, ctx) =>
        {
            ctx.State.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
            return Task.CompletedTask;
        });
        gatedManager.Release();
        await saveTask;
        bool hasUnsavedChanges = await session.InvokeAsync(
            (_, ctx) => Task.FromResult(ctx.State.HasUnsavedChanges));

        // Assert
        hasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WhenRootStackClearedDuringWrite_KeepsUnsavedChangesFlag()
    {
        // Arrange — the saved (non-empty) stack is cleared while the write is in flight
        GatedRecipeManager gatedManager = new();
        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await StartGatedSaveSessionAsync(_csvFile, _recipeFile, gatedManager);
        Task saveTask = session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());
        await gatedManager.WriteStarted;

        // Act
        await session.InvokeAsync((_, ctx) =>
        {
            ctx.State.ClearMorphActions();
            return Task.CompletedTask;
        });
        gatedManager.Release();
        await saveTask;
        bool hasUnsavedChanges = await session.InvokeAsync(
            (_, ctx) => Task.FromResult(ctx.State.HasUnsavedChanges));

        // Assert
        hasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ThenRootEdit_MarksRootUnsavedAgain()
    {
        // Arrange — dirty root, saved successfully (clean)
        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, _recipeFile);

            IRowIndexer indexer = RowIndexerFactory.Create(DataFormat.Csv, _csvFile);
            TableSchema csvSchema = CreateCsvTextColumnSchema();
            AppState state = new();
            state.StartNewFile(_csvFile);
            state.CompleteCsvLoad(indexer, csvSchema);
            state.AddMorphAction(new RenameColumnAction { OldName = "old", NewName = "new" });
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());

        // Act
        (bool flagAfterSave, bool flagAfterEdit) = await session.InvokeAsync((_, ctx) =>
        {
            bool afterSave = ctx.State.HasUnsavedChanges;
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

    [Fact]
    public async Task SaveAsync_FromFocusedTable_ThenDrillDownEdit_MarksDrillDownUnsavedAgain()
    {
        // Arrange — dirty DrillDown session, saved successfully (clean)
        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, _recipeFile);

            DrillDownState drillDown = CreateDirtyDrillDown();
            AppState state = new();
            state.StartNewFile(_jsonLinesFile);
            state.EnterFocusedTable(drillDown);
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            viewManager.SwitchToFocusedTable(drillDown);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());

        // Act
        (bool? flagAfterSave, bool? flagAfterEdit) = await session.InvokeAsync((_, ctx) =>
        {
            bool? afterSave = ctx.State.GetDrillDownOrNull()?.HasUnsavedChanges;
            FocusedTableView view = ctx.ViewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
            view.OnMorphAction?.Invoke(new RenameColumnAction { OldName = "col1", NewName = "again" });
            return Task.FromResult((afterSave, ctx.State.GetDrillDownOrNull()?.HasUnsavedChanges));
        });

        // Assert
        flagAfterSave.Should().BeFalse();
        flagAfterEdit.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_FromFocusedTableWithFailedWrite_EndsDrillDownSession()
    {
        // Arrange — the dialog path targets a missing directory, so the DrillDown save fails and
        // the error view replaces FocusedTable, which ends the session
        string unreachableRecipeFile = Path.Combine(
            Path.GetTempPath(), $"refedle-missing-{Guid.NewGuid():N}", "recipe.yaml");
        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, unreachableRecipeFile);

            DrillDownState drillDown = CreateDirtyDrillDown();
            AppState state = new();
            state.StartNewFile(_jsonLinesFile);
            state.EnterFocusedTable(drillDown);
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());
        DrillDownState? drillDownAfter = await session.InvokeAsync(
            (_, ctx) => Task.FromResult(ctx.State.GetDrillDownOrNull()));

        // Assert
        drillDownAfter.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WithFailedWrite_KeepsRootUnsavedChangesFlag()
    {
        // Arrange — the dialog path targets a missing directory, so RecipeManager.SaveAsync fails
        // and the unsaved flag must survive the error path
        string unreachableRecipeFile = Path.Combine(
            Path.GetTempPath(), $"refedle-missing-{Guid.NewGuid():N}", "recipe.yaml");
        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, unreachableRecipeFile);

            IRowIndexer indexer = RowIndexerFactory.Create(DataFormat.Csv, _csvFile);
            TableSchema csvSchema = CreateCsvTextColumnSchema();
            AppState state = new();
            state.StartNewFile(_csvFile);
            state.CompleteCsvLoad(indexer, csvSchema);
            state.AddMorphAction(new RenameColumnAction { OldName = "old", NewName = "new" });
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.SaveAsync());
        (bool hasUnsavedChanges, bool isPlaceholder) = await session.InvokeAsync(
            (_, ctx) => Task.FromResult((ctx.State.HasUnsavedChanges, ctx.ViewManager.GetCurrentView() is PlaceholderView)));

        // Assert — the write failed (error placeholder shown), so the flag must stay set
        isPlaceholder.Should().BeTrue();
        hasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_WithAcceptedDialog_LoadsRecipeFromSelectedPath()
    {
        // Arrange
        RenameColumnAction action = new() { OldName = "old", NewName = "new" };
        await SaveRecipeAsync(new Recipe { Name = "test", Actions = [action] }, _recipeFile);

        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, _recipeFile);

            AppState state = new();
            state.StartNewFile(_jsonLinesFile);
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act — drive the real Load dialog to acceptance, exercising the LoadAsync continuation
        // that reads dialog.Canceled/dialog.Path before delegating to LoadFromPathAsync.
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadAsync());
        IMorphAction[] actionStack = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.State.ActionStack.ToArray()));

        // Assert
        actionStack.Should().Equal(action);
    }
}

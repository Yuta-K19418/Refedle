using AwesomeAssertions;
using Refedle.App;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Recipes;
using Refedle.Engine.Types;
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
        using var state = new AppState { CurrentMode = ViewMode.FileSelection };
        using var window = new Window();
        var modeController = new ModeController(state, action => action());
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
        using var state = new AppState
        {
            CurrentFilePath = "data.jsonl",
            CurrentMode = ViewMode.FocusedTable,
            DrillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                keyPath,
                ActionStack: [drillDownAction]),
        };
        state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
        using var window = new Window();
        var modeController = new ModeController(state, action => action());
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
        // Arrange — a stale DrillDown (left over from Backspace navigation) must be ignored when
        // the current view is the base table, not FocusedTable
        using var app = CreateTestApp();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        var baseAction = new RenameColumnAction { OldName = "base", NewName = "renamed_base" };
        using var state = new AppState
        {
            CurrentFilePath = "data.jsonl",
            CurrentMode = ViewMode.JsonLinesTable,
            DrillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                KeyPath: [new KeyPathSegment("stale", KeyPathSegmentKind.Key)],
                ActionStack: [new RenameColumnAction { OldName = "stale", NewName = "stale_renamed" }]),
        };
        state.AddMorphAction(baseAction);
        using var window = new Window();
        var modeController = new ModeController(state, action => action());
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
        using var state = new AppState { CurrentFilePath = string.Empty };
        using var window = new Window();
        var modeController = new ModeController(state, action => action());
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

            var state = new AppState { CurrentFilePath = _csvFile, CurrentMode = ViewMode.CsvTable };
            state.AddMorphAction(action);
            var modeController = new ModeController(state, action => action());
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
    public async Task LoadAsync_WithAcceptedDialog_LoadsRecipeFromSelectedPath()
    {
        // Arrange
        var action = new RenameColumnAction { OldName = "old", NewName = "new" };
        await SaveRecipeAsync(new Recipe { Name = "test", Actions = [action] }, _recipeFile);

        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AcceptModalDialogs(app, window, _recipeFile);

            var state = new AppState { CurrentFilePath = _jsonLinesFile };
            var modeController = new ModeController(state, action => action());
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

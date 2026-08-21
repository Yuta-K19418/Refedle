using AwesomeAssertions;
using Refedle.App;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
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

    [Fact]
    public async Task SaveAsync_WithNonTableMode_DoesNothing()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentMode = ViewMode.FileSelection };
        using var window = new Window();
        var modeController = new ModeController(state);
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
        var modeController = new ModeController(state);
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
        var modeController = new ModeController(state);
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
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var handler = new RecipeCommandHandler(app, state, viewManager);

        // Act
        Func<Task> act = async () => await handler.LoadAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}

using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui.UI;
using Refedle.TestHelpers.App.Tui.Workers.Schema;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

public sealed partial class RecipeCommandHandlerTests
{
    [Fact]
    public async Task SaveAsync_WithNonTableMode_DoesNothing()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        RecipeCommandHandler handler = new(app, state, viewManager);

        // Act
        Func<Task> act = async () => await handler.SaveAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void BuildRecipe_FromFocusedTableWithActiveDrillDown_UsesDrillDownKeyPathAndActionStack()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        TableSchema schema = new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("orders", KeyPathSegmentKind.Key)];
        RenameColumnAction drillDownAction = new() { OldName = "drill", NewName = "renamed_drill" };
        using AppState state = new();
        state.StartNewFile("data.jsonl");
        DrillDownState drillDown = new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            schema,
            ViewMode.JsonLinesTree,
            keyPath,
            ActionStack: [drillDownAction]);
        state.EnterFocusedTable(drillDown);
        state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        RecipeCommandHandler handler = new(app, state, viewManager);

        // Act
        Recipe recipe = handler.BuildRecipe();

        // Assert
        recipe.DrillDownKeyPath.Should().Equal(keyPath);
        recipe.Actions.Should().Equal(drillDownAction);
    }

    [Fact]
    public void BuildRecipe_AfterErrorViewReplacedFocusedTable_UsesBaseActionStackAndOmitsDrillDownKeyPath()
    {
        // Arrange — the DrillDown's scope must not be captured once an error view has replaced
        // FocusedTable
        using IApplication app = TestAppFactory.Create();
        TableSchema schema = new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        RenameColumnAction baseAction = new() { OldName = "base", NewName = "renamed_base" };
        using AppState state = new();
        state.StartNewFile("data.jsonl");
        DrillDownState drillDown = new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            schema,
            ViewMode.JsonLinesTree,
            KeyPath: [new KeyPathSegment("stale", KeyPathSegmentKind.Key)],
            ActionStack: [new RenameColumnAction { OldName = "stale", NewName = "stale_renamed" }]);
        state.EnterFocusedTable(drillDown);
        state.EnterPlaceholderMode();
        state.AddMorphAction(baseAction);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        RecipeCommandHandler handler = new(app, state, viewManager);

        // Act
        Recipe recipe = handler.BuildRecipe();

        // Assert
        recipe.DrillDownKeyPath.Should().BeNull();
        recipe.Actions.Should().Equal(baseAction);
    }

    [Fact]
    public async Task LoadAsync_WithNoFilePath_DoesNothing()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        RecipeCommandHandler handler = new(app, state, viewManager);

        // Act
        Func<Task> act = async () => await handler.LoadAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}

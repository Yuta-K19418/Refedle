using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Views;
using Refedle.App.Views.JsonTreeNodes;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.Tests.App;

public sealed class AppKeyHandlerTests
{
    [Theory]
    [InlineData(KeyCode.O)]
    [InlineData(KeyCode.S)]
    [InlineData(KeyCode.Q)]
    [InlineData(KeyCode.T)]
    [InlineData(KeyCode.X)]
    [InlineData(KeyCode.C)]
    [InlineData(KeyCode.Backspace)]
    [InlineData((KeyCode)'?')]
    public void IsGlobalShortcut_WithGlobalShortcutKeys_ReturnsTrue(KeyCode keyCode)
    {
        // Arrange
        // Act
        var result = AppKeyHandler.IsGlobalShortcut(keyCode);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(KeyCode.A)]
    [InlineData(KeyCode.B)]
    [InlineData(KeyCode.Z)]
    [InlineData((KeyCode)'1')]
    public void IsGlobalShortcut_WithNonGlobalShortcutKeys_ReturnsFalse(KeyCode keyCode)
    {
        // Arrange
        // Act
        var result = AppKeyHandler.IsGlobalShortcut(keyCode);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(KeyCode.O | KeyCode.CtrlMask)]
    [InlineData(KeyCode.S | KeyCode.CtrlMask)]
    [InlineData(KeyCode.Q | KeyCode.CtrlMask)]
    [InlineData(KeyCode.T | KeyCode.CtrlMask)]
    [InlineData(KeyCode.X | KeyCode.CtrlMask)]
    [InlineData(KeyCode.C | KeyCode.CtrlMask)]
    [InlineData(KeyCode.Backspace | KeyCode.CtrlMask)]
    [InlineData((KeyCode)'?' | KeyCode.CtrlMask)]
    public void IsGlobalShortcut_WithModifierKeys_ReturnsTrue(KeyCode keyCode)
    {
        // Arrange
        // Act
        var result = AppKeyHandler.IsGlobalShortcut(keyCode);

        // Assert
        // Modifier keys are ignored - only the base character is checked
        result.Should().BeTrue();
    }

    [Fact]
    public void HandleActionMenu_WhenCurrentViewIsNotMorphTableView_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        var result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleActionMenu_WhenTableIsNull_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        using var view = new TestTableView { Table = null };
        window.Add(view);
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        var result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleActionMenu_WhenGetRawColumnNameIsNull_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        using var view = new TestTableView { Table = new TestTableSource() };
        window.Add(view);
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        var result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleActionMenu_WhenOnMorphActionIsNull_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        using var view = new TestTableView
        {
            Table = new TestTableSource(),
            GetRawColumnName = _ => "test"
        };
        window.Add(view);
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        var result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleActionMenu_WhenSelectedColumnIsNegative_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        using var view = new TestTableView
        {
            Table = new TestTableSource(),
            GetRawColumnName = _ => "test",
            OnMorphAction = _ => { }
        };
        view.Value = null;
        window.Add(view);
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        var result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleClearActions_WhenActionStackIsEmpty_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        var result = handler.HandleClearActions();

        // Assert
        result.Should().BeFalse();
        state.ActionStack.Should().BeEmpty();
    }

    [Fact]
    public void HandleClearActions_WhenFocusedTableDrillDownActionStackEmpty_ReturnsFalse()
    {
        // Arrange — base ActionStack has entries, but the active DrillDown's own stack is empty;
        // clearing from FocusedTable must consult the DrillDown's stack, not the base one
        using var app = CreateTestApp();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState { CurrentMode = ViewMode.FocusedTable };
        state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
        state.DrillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonLinesTree, KeyPath: [], ActionStack: []);
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        var result = handler.HandleClearActions();

        // Assert
        result.Should().BeFalse();
        state.ActionStack.Should().ContainSingle();
    }

    [Fact]
    public void HandleClearActions_WhenBaseActionStackEmptyAndStaleDrillDownHasActions_ReturnsFalse()
    {
        // Arrange — a stale DrillDown (left over from Backspace navigation) carries actions, but the
        // current view is the base table; clearing must consult the base stack, not the stale one
        using var app = CreateTestApp();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState
        {
            DrillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                KeyPath: [],
                ActionStack: [new RenameColumnAction { OldName = "x", NewName = "y" }]),
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        var result = handler.HandleClearActions();

        // Assert
        result.Should().BeFalse();
        var drillDown = state.DrillDown.Should().BeOfType<DrillDownState>().Which;
        drillDown.ActionStack.Should().ContainSingle();
    }

    // The confirmed-clear branches (MessageBox.Query "Yes") require a TUI event loop to
    // auto-dismiss the dialog; see MainWindowTests.KeyDown_WithCKey_* for those.

    [Fact]
    public void OnGlobalKeyDown_BackspaceInFocusedTableWithDrillDown_CallsReturnFromDrillDown()
    {
        // Arrange
        using var app = CreateTestApp();
        IReadOnlyList<JsonObjectEntry> entries = [new JsonObjectEntry("id", "1"u8.ToArray())];
        var schema = new TableSchema { SourceFormat = DataFormat.JsonObject, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState
        {
            CurrentFilePath = "test.json",
            CurrentMode = ViewMode.FocusedTable,
            JsonObjectEntries = entries,
            DrillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonObjectTree, KeyPath: [], ActionStack: []),
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler);
        handler.Subscribe();

        // Act
        var handled = app.Keyboard.RaiseKeyDownEvent(KeyCode.Backspace);

        // Assert
        handled.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonObjectTree);
        state.DrillDown.Should().BeNull();
    }

    [Fact]
    public void OnGlobalKeyDown_BackspaceOutsideFocusedTable_ReturnsUnhandled()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentMode = ViewMode.FileSelection };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler);
        handler.Subscribe();

        // Act
        var handled = app.Keyboard.RaiseKeyDownEvent(KeyCode.Backspace);

        // Assert
        handled.Should().BeFalse();
    }

    [Fact]
    public void HandleActionMenu_WithJsonObjectFormatAndArrayNode_DispatchesSingleDrillDown()
    {
        // Arrange
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, "{\"orders\":[{\"id\":1},{\"id\":2}]}");
        try
        {
            using var app = CreateTestApp();
            using var state = new AppState { CurrentFilePath = filePath };
            using var window = new Window();
            var modeController = new ModeController(state);
            using var viewManager = new ViewManager(window, state, modeController, action => action());
            viewManager.SwitchToJsonObjectTree([new JsonObjectEntry("orders", "[{\"id\":1},{\"id\":2}]"u8.ToArray())]);
            var treeView = (MorphTreeView)viewManager.GetCurrentView()!;
            treeView.SelectedObject = JsonObjectTreeView.CreateKeyNode("orders", "[{\"id\":1},{\"id\":2}]"u8.ToArray());
            var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });
            var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
            using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler);
            app.Iteration += (_, _) => app.Keyboard.RaiseKeyDownEvent(Key.Enter);

            // Act — HandleActionMenu detects JsonObject format and dispatches to HandleSingleDrillDown,
            // which runs an ActionMenuDialog confirmed here via the Enter-key pattern.
            var result = handler.HandleActionMenu();

            // Assert
            result.Should().BeTrue();
            state.CurrentMode.Should().Be(ViewMode.FocusedTable);
            viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidSingleDrillDownSelections))]
    public void HandleActionMenu_WhenSelectedNodeInvalidForSingleDrillDown_ReturnsFalse(ITreeNode selectedNode)
    {
        // Arrange — drive the guards indirectly through HandleActionMenu (same pattern as
        // HandleActionMenu_WhenCurrentViewIsNotMorphTableView_ReturnsFalse). The guard clauses
        // return before the ActionMenuDialog is shown, so no Enter-key confirmation is needed.
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, "{\"orders\":[{\"id\":1},{\"id\":2}]}");
        try
        {
            using var app = CreateTestApp();
            using var state = new AppState { CurrentFilePath = filePath };
            using var window = new Window();
            var modeController = new ModeController(state);
            using var viewManager = new ViewManager(window, state, modeController, action => action());
            viewManager.SwitchToJsonObjectTree([new JsonObjectEntry("orders", "[{\"id\":1},{\"id\":2}]"u8.ToArray())]);
            var treeView = (MorphTreeView)viewManager.GetCurrentView()!;
            treeView.SelectedObject = selectedNode;
            var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { }, () => { });
            var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
            using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

            // Act
            var result = handler.HandleActionMenu();

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// One ITreeNode fixture per HandleSingleDrillDown guard clause (see the guard comments below for details).
    /// </summary>
    public static IEnumerable<object[]> InvalidSingleDrillDownSelections()
    {
        // Guard 1: a primitive value node is not a JsonArrayTreeNode.
        yield return [(new JsonValueTreeNode("not-an-array"))];

        // Guard 2: array whose lazy Children parse to zero elements.
        yield return [(new JsonArrayTreeNode("[]"u8.ToArray()))];

        // Guard 3: mixed-type children (object + value) fail the "all children must be objects" check.
        yield return [(new JsonArrayTreeNode("[{},1]"u8.ToArray()))];
    }

    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        Assert.NotNull(app.Driver);
        app.Driver.SetScreenSize(80, 25);
        return app;
    }

    /// <summary>
    /// Testable concrete implementation of MorphTableView.
    /// </summary>
    private sealed class TestTableView : MorphTableView
    {
        public new ITableSource? Table { get; set; }
    }

    /// <summary>
    /// Simple TableSource implementation for testing.
    /// </summary>
    private sealed class TestTableSource : ITableSource
    {
        public int Rows => 10;
        public int Columns => 3;
        public string[] ColumnNames => ["Col1", "Col2", "Col3"];

        public object this[int row, int col]
        {
            get => $"R{row}C{col}";
            set { }
        }

        public static void AddColumn(string _) { }
        public static void AddRow() { }
        public static void RemoveColumn(int _) { }
        public static void RemoveRow(int _) { }
        public static void Clear() { }
    }
}

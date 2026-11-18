using System.Drawing;
using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.UI.Views.JsonRangeTreeNodes;
using Refedle.App.Tui.UI.Views.JsonTreeNodes;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui;
using Refedle.TestHelpers.App.Tui.UI;
using Refedle.TestHelpers.App.Tui.Workers.Schema;
using Refedle.TestHelpers.IO;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

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
        bool result = AppKeyHandler.IsGlobalShortcut(keyCode);

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
        bool result = AppKeyHandler.IsGlobalShortcut(keyCode);

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
        bool result = AppKeyHandler.IsGlobalShortcut(keyCode);

        // Assert
        // Modifier keys are ignored - only the base character is checked
        result.Should().BeTrue();
    }

    [Fact]
    public void HandleActionMenu_WhenCurrentViewIsNotMorphTableView_ReturnsFalse()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
        RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
        using AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        bool result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleActionMenu_WhenTableIsNull_ReturnsFalse()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        using PlainMorphTableView view = new() { Table = null };
        window.Add(view);
        FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
        RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
        using AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        bool result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleActionMenu_WhenTableIsNotPagedTableSource_ReturnsFalse()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        using PlainMorphTableView view = new()
        {
            Table = new NonExtendedTableSource(),
            OnMorphAction = _ => { },
            // A cursor-only selection on column 0 — SetSelection alone does not populate Value
            Value = new TableSelection(new Point(0, 0)),
        };
        window.Add(view);
        FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
        RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
        using AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        bool result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleActionMenu_WhenOnMorphActionIsNull_ReturnsFalse()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        using PlainMorphTableView view = new() { Table = new PagedTableSource(new TestTableSource()) };
        window.Add(view);
        FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
        RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
        using AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        bool result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleActionMenu_WhenTableValueIsNull_ReturnsFalse()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        using PlainMorphTableView view = new()
        {
            Table = new PagedTableSource(new TestTableSource()),
            OnMorphAction = _ => { }
        };
        view.Value = null;
        window.Add(view);
        FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
        RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
        using AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        bool result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleClearActions_WhenActionStackIsEmpty_ReturnsFalse()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
        RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
        using AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        bool result = handler.HandleClearActions();

        // Assert
        result.Should().BeFalse();
        state.ActionStack.Should().BeEmpty();
    }

    [Fact]
    public void HandleClearActions_WhenFocusedTableDrillDownActionStackEmpty_ReturnsFalse()
    {
        // Arrange — base ActionStack has entries, but the active DrillDown's own stack is empty;
        // clearing from FocusedTable must consult the DrillDown's stack, not the base one
        using IApplication app = TestAppFactory.Create();
        TableSchema schema = new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using AppState state = new();
        state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
        DrillDownState drillDown = new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonLinesTree, KeyPath: [], ActionStack: []);
        state.EnterFocusedTable(drillDown);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
        RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
        using AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        bool result = handler.HandleClearActions();

        // Assert
        result.Should().BeFalse();
        state.ActionStack.Should().ContainSingle();
    }

    [Fact]
    public void HandleClearActions_WhenErrorViewReplacedFocusedTableAndBaseActionStackEmpty_ReturnsFalse()
    {
        // Arrange — the DrillDown carried actions before an error view replaced FocusedTable;
        // clearing must consult the (empty) base stack, not those actions
        using IApplication app = TestAppFactory.Create();
        TableSchema schema = new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using AppState state = new();
        DrillDownState drillDown = new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            schema,
            ViewMode.JsonLinesTree,
            KeyPath: [],
            ActionStack: [new RenameColumnAction { OldName = "x", NewName = "y" }]);
        state.EnterFocusedTable(drillDown);
        state.EnterPlaceholderMode();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
        RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
        using AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        bool result = handler.HandleClearActions();

        // Assert
        result.Should().BeFalse();
        state.TryGetDrillDown(out _).Should().BeFalse();
    }

    // The confirmed-clear branches (MessageBox.Query "Yes") require a TUI event loop to
    // auto-dismiss the dialog; see MainWindowTests.KeyDown_WithCKey_* for those.

    [Fact]
    public void OnGlobalKeyDown_BackspaceInFocusedTableWithDrillDown_CallsReturnFromDrillDown()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        IReadOnlyList<JsonObjectEntry> entries = [new JsonObjectEntry("id", "1"u8.ToArray())];
        TableSchema schema = new() { SourceFormat = DataFormat.JsonObject, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using AppState state = new();
        state.StartNewFile("test.json");
        state.EnterJsonObjectTree(entries);
        DrillDownState drillDown = new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonObjectTree, KeyPath: [], ActionStack: []);
        state.EnterFocusedTable(drillDown);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
        RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
        using AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);
        handler.Subscribe();

        // Act
        bool handled = app.Keyboard.RaiseKeyDownEvent(KeyCode.Backspace);

        // Assert
        handled.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonObjectTree);
        state.GetDrillDownOrNull().Should().BeNull();
    }

    [Fact]
    public void OnGlobalKeyDown_BackspaceOutsideFocusedTable_ReturnsUnhandled()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
        RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
        using AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);
        handler.Subscribe();

        // Act
        bool handled = app.Keyboard.RaiseKeyDownEvent(KeyCode.Backspace);

        // Assert
        handled.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(InvalidSingleDrillDownSelections))]
    public void HandleActionMenu_WhenSelectedNodeInvalidForSingleDrillDown_ReturnsFalse(ITreeNode selectedNode)
    {
        // Arrange — drive the guards indirectly through HandleActionMenu (same pattern as
        // HandleActionMenu_WhenCurrentViewIsNotMorphTableView_ReturnsFalse). The guard clauses
        // return before the ActionMenuDialog is shown, so no Enter-key confirmation is needed.
        using TempFile tempFile = TempFile.ReservePath(".json");
        string filePath = tempFile.Path;
        File.WriteAllText(filePath, "{\"orders\":[{\"id\":1},{\"id\":2}]}");
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.StartNewFile(filePath);
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        viewManager.SwitchToJsonObjectTree([new JsonObjectEntry("orders", "[{\"id\":1},{\"id\":2}]"u8.ToArray())]);
        MorphTreeView treeView = (MorphTreeView)viewManager.GetCurrentView()!;
        treeView.SelectedObject = selectedNode;
        FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
        RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
        using AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);

        // Act
        bool result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// One ITreeNode fixture per HandleSingleDrillDown guard clause (see the guard comments below for details).
    /// </summary>
    public static IEnumerable<object[]> InvalidSingleDrillDownSelections()
    {
        // Guard 1: a primitive value node is not a JsonArrayTreeNode.
        yield return [(new JsonValueTreeNode("not-an-array"))];

        // Guard 1b: a JSON Object range node is not a JsonArrayTreeNode,
        // so selecting it must not open the action menu.
        yield return [(new JsonObjectRangeTreeNode([new JsonObjectEntry("k", "1"u8.ToArray())], 0L, 1L))];

        // Guard 2: array whose lazy Children parse to zero elements.
        yield return [(new JsonArrayTreeNode("[]"u8.ToArray()))];

        // Guard 3: mixed-type children (object + value) fail the "all children must be objects" check.
        yield return [(new JsonArrayTreeNode("[{},1]"u8.ToArray()))];
    }

    /// <summary>
    /// Simple TenRowTableSource implementation for testing.
    /// </summary>
    private sealed class TestTableSource : IExtendedTableSource
    {
        public long TotalRows => 10;
        public int Columns => 3;
        public string[] ColumnNames => ["Col1", "Col2", "Col3"];
        public string[] RawColumnNames => ["Raw1", "Raw2", "Raw3"];
        public object this[long row, int col] => $"R{row}C{col}";

        public static void AddColumn(string _) { }
        public static void AddRow() { }
        public static void RemoveColumn(int _) { }
        public static void RemoveRow(int _) { }
        public static void Clear() { }
    }

    /// <summary>
    /// Bare <see cref="ITableSource"/> without the extended contract, for the guard test.
    /// </summary>
    private sealed class NonExtendedTableSource : ITableSource
    {
        public int Rows => 10;
        public int Columns => 3;
        public string[] ColumnNames => ["Col1", "Col2", "Col3"];

        public object this[int row, int col]
        {
            get => $"R{row}C{col}";
            set { }
        }
    }
}

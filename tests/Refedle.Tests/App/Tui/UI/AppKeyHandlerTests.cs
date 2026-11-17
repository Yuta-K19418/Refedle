using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.UI.Views.Dialogs;
using Refedle.App.Tui.UI.Views.JsonRangeTreeNodes;
using Refedle.App.Tui.UI.Views.JsonTreeNodes;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonArray;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.Tests.App.Tui.Workers.Schema;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using CsvRowIndexer = Refedle.Engine.IO.Csv.DataRowIndexer;
using JsonArrayRowIndexer = Refedle.Engine.IO.JsonArray.RowIndexer;
using JsonLinesRowIndexer = Refedle.Engine.IO.JsonLines.RowIndexer;

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
        using TestTableView view = new() { Table = null };
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
        using TestTableView view = new()
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
        using TestTableView view = new() { Table = new PagedTableSource(new TestTableSource()) };
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
        using TestTableView view = new()
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

    [Fact]
    public async Task HandleActionMenu_WhenTableActionSelected_ClosesMenuBeforeActionDialogOpens()
    {
        // Arrange
        string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
        File.WriteAllText(filePath, "Col1,Col2,Col3\n1,2,3\n");
        try
        {
            MenuClosedProbe probe = new();
            await using LivePumpTestSession<LiveTestContext<AppKeyHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
            {
                LiveTestContext<AppKeyHandler> context = CreateTableMenuContext(app, window, filePath);
                probe.Attach(app);
                return context;
            });

            // Act
            bool result = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.Handler.HandleActionMenu()));

            // Assert
            result.Should().BeTrue();
            probe.MenuRunningWhenActionDialogOpened.Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task HandleActionMenu_WhenTableMenuCancelled_RunsNoAction()
    {
        // Arrange
        string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
        File.WriteAllText(filePath, "Col1,Col2,Col3\n1,2,3\n");
        try
        {
            await using LivePumpTestSession<LiveTestContext<AppKeyHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
            {
                LiveTestContext<AppKeyHandler> context = CreateTableMenuContext(app, window, filePath);
                ModalKeys.SendOnceOnTop(app, window, Key.Esc);
                return context;
            });

            // Act
            bool result = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.Handler.HandleActionMenu()));

            // Assert
            int actionCount = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.State.ActionStack.Count));
            result.Should().BeTrue();
            actionCount.Should().Be(0);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Confirms the first action in the Actions menu, then records whether the menu was still
    /// running at the moment the action's dialog became the top view, and dismisses that dialog.
    /// </summary>
    private sealed class MenuClosedProbe
    {
        private ActionMenuDialog? _menu;

        public bool? MenuRunningWhenActionDialogOpened { get; private set; }

        public void Attach(IApplication app)
        {
            void OnIteration(object? sender, EventArgs<IApplication?> args)
            {
                View? topView = app.TopRunnableView;
                if (topView is ActionMenuDialog top && _menu is null)
                {
                    _menu = top;
                    app.Keyboard.RaiseKeyDownEvent(Key.Enter);
                }
                else if (topView is RenameColumnDialog)
                {
                    app.Iteration -= OnIteration;
                    MenuRunningWhenActionDialogOpened = _menu?.IsRunning;
                    app.Keyboard.RaiseKeyDownEvent(Key.Esc);
                }
            }

            app.Iteration += OnIteration;
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "The session disposes the returned context, the window owns the view, and the handler is owned by the test host.")]
    private static LiveTestContext<AppKeyHandler> CreateTableMenuContext(
        IApplication app, Window window, string filePath)
    {
        AppState state = new();
        state.StartNewFile(filePath);
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
        CsvRowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.Csv,
            Columns = [new ColumnSchema { Name = "Col1", Type = ColumnType.Text }],
        };
        viewManager.SwitchToCsvTable(indexer, schema);
        CsvTableView view = viewManager.GetCurrentView().Should().BeOfType<CsvTableView>().Which;
        view.Value = new TableSelection(new Point(0, 0));
        FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
        RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
        AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);
        return new LiveTestContext<AppKeyHandler>(state, viewManager, handler);
    }

    [Fact]
    public async Task HandleActionMenu_WithJsonObjectFormatAndArrayNode_DispatchesSingleDrillDown()
    {
        // Arrange
        string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, "{\"orders\":[{\"id\":1},{\"id\":2}]}");
        try
        {
            await using LivePumpTestSession<LiveTestContext<AppKeyHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
            {
                AppState state = new();
                state.StartNewFile(filePath);
                ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
                ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
                viewManager.SwitchToJsonObjectTree([new JsonObjectEntry("orders", "[{\"id\":1},{\"id\":2}]"u8.ToArray())]);
                MorphTreeView treeView = (MorphTreeView)viewManager.GetCurrentView()!;
                treeView.SelectedObject = JsonObjectRangeTreeNode.CreateKeyNode("orders", "[{\"id\":1},{\"id\":2}]"u8.ToArray());
                FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
                RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
                AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);
                ModalKeys.SendOnceOnTop(app, window, Key.Enter);
                return new LiveTestContext<AppKeyHandler>(state, viewManager, handler);
            });

            // Act — HandleActionMenu detects JsonObject format and dispatches to HandleSingleDrillDown,
            // which runs an ActionMenuDialog confirmed here via the Enter-key pattern.
            bool result = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.Handler.HandleActionMenu()));

            // Assert
            result.Should().BeTrue();
            (ViewMode mode, Type? viewType) = await session.InvokeAsync((_, ctx) =>
                Task.FromResult((ctx.State.CurrentMode, ctx.ViewManager.GetCurrentView()?.GetType())));
            mode.Should().Be(ViewMode.FocusedTable);
            viewType.Should().Be<FocusedTableView>();
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
        string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, "{\"orders\":[{\"id\":1},{\"id\":2}]}");
        try
        {
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

        // Guard 1b: a JSON Object range node is not a JsonArrayTreeNode,
        // so selecting it must not open the action menu.
        yield return [(new JsonObjectRangeTreeNode([new JsonObjectEntry("k", "1"u8.ToArray())], 0L, 1L))];

        // Guard 2: array whose lazy Children parse to zero elements.
        yield return [(new JsonArrayTreeNode("[]"u8.ToArray()))];

        // Guard 3: mixed-type children (object + value) fail the "all children must be objects" check.
        yield return [(new JsonArrayTreeNode("[{},1]"u8.ToArray()))];
    }

    [Fact]
    public async Task HandleActionMenu_WhenJsonLinesRangeNodeSelected_ReturnsFalse()
    {
        // Arrange — a range node is a paging container whose KeyPath is empty, so the action
        // menu must not open for it. Enter auto-closes the dialog if the guard ever regresses,
        // making the assertion fail instead of the test hanging on the modal loop.
        string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jsonl");
        File.WriteAllText(filePath, "{\"id\":1}\n{\"id\":2}");
        try
        {
            // The test owns the reader behind the range node; declaring it before the session
            // makes it dispose after the session ends but before the temp file is deleted.
            using RowReader reader = new(filePath);
            await using LivePumpTestSession<LiveTestContext<AppKeyHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
            {
                AppState state = new();
                state.StartNewFile(filePath);
                ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
                ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
                JsonLinesRowIndexer indexer = new(filePath);
                indexer.BuildIndex();
                viewManager.SwitchToJsonLinesTree(indexer);
                MorphTreeView treeView = GetCurrentTreeViewOrThrow(viewManager, "SwitchToJsonLinesTree");
                treeView.SelectedObject = new JsonLinesRangeTreeNode(indexer, reader, 0L, 2L);
                FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
                RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
                AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);
                ModalKeys.SendOnceOnTop(app, window, Key.Enter);
                return new LiveTestContext<AppKeyHandler>(state, viewManager, handler);
            });

            // Act
            bool result = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.Handler.HandleActionMenu()));

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task HandleActionMenu_WhenJsonArrayRangeNodeSelected_ReturnsFalse()
    {
        // Arrange — same regression guard as the JSON Lines case, on the JSON Array tree view.
        string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, "[1,2]");
        try
        {
            // The test owns the reader behind the range node; declaring it before the session
            // makes it dispose after the session ends but before the temp file is deleted.
            using ElementReader reader = new(filePath);
            await using LivePumpTestSession<LiveTestContext<AppKeyHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
            {
                AppState state = new();
                state.StartNewFile(filePath);
                ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
                ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());
                JsonArrayRowIndexer indexer = new(filePath);
                indexer.BuildIndex();
                viewManager.SwitchToJsonArrayTree(indexer);
                MorphTreeView treeView = GetCurrentTreeViewOrThrow(viewManager, "SwitchToJsonArrayTree");
                treeView.SelectedObject = new JsonArrayRangeTreeNode(indexer, reader, 0L, 2L);
                FileDialogHandler fileDialogHandler = new(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
                RecipeCommandHandler recipeCommandHandler = new(app, state, viewManager);
                AppKeyHandler handler = new(app, state, viewManager, fileDialogHandler, recipeCommandHandler);
                ModalKeys.SendOnceOnTop(app, window, Key.Enter);
                return new LiveTestContext<AppKeyHandler>(state, viewManager, handler);
            });

            // Act
            bool result = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.Handler.HandleActionMenu()));

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Returns the current view as a <see cref="MorphTreeView"/>, or throws when the tree
    /// switch failed to install one — a broken setup, not a nullable expectation.
    /// </summary>
    private static MorphTreeView GetCurrentTreeViewOrThrow(ViewManager viewManager, string treeSwitchName)
    {
        View? currentView = viewManager.GetCurrentView();
        if (currentView is not MorphTreeView treeView)
        {
            throw new InvalidOperationException(
                $"{treeSwitchName} must install a MorphTreeView as the current view, but found: {currentView?.ToString() ?? "null"}");
        }

        return treeView;
    }

    /// <summary>
    /// Testable concrete implementation of MorphTableView.
    /// </summary>
    private sealed class TestTableView : MorphTableView;

    /// <summary>
    /// Simple TableSource implementation for testing.
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

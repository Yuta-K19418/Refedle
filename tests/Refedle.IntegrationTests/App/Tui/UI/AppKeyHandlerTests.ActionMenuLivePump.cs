using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.UI.Views.Dialogs;
using Refedle.App.Tui.UI.Views.JsonRangeTreeNodes;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO.JsonArray;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui.Workers.Schema;
using Refedle.TestHelpers.IO;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using CsvRowIndexer = Refedle.Engine.IO.Csv.DataRowIndexer;
using JsonArrayRowIndexer = Refedle.Engine.IO.JsonArray.RowIndexer;
using JsonLinesRowIndexer = Refedle.Engine.IO.JsonLines.RowIndexer;

namespace Refedle.IntegrationTests.App.Tui.UI;

public sealed class AppKeyHandlerTests
{
    [Fact]
    public async Task HandleActionMenu_WhenTableActionSelected_ClosesMenuBeforeActionDialogOpens()
    {
        // Arrange
        using TempFile tempFile = TempFile.ReservePath(".csv");
        string filePath = tempFile.Path;
        File.WriteAllText(filePath, "Col1,Col2,Col3\n1,2,3\n");
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

    [Fact]
    public async Task HandleActionMenu_WhenTableMenuCancelled_RunsNoAction()
    {
        // Arrange
        using TempFile tempFile = TempFile.ReservePath(".csv");
        string filePath = tempFile.Path;
        File.WriteAllText(filePath, "Col1,Col2,Col3\n1,2,3\n");
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
        using TempFile tempFile = TempFile.ReservePath(".json");
        string filePath = tempFile.Path;
        File.WriteAllText(filePath, "{\"orders\":[{\"id\":1},{\"id\":2}]}");
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

    [Fact]
    public async Task HandleActionMenu_WhenJsonLinesRangeNodeSelected_ReturnsFalse()
    {
        // Arrange — a range node is a paging container whose KeyPath is empty, so the action
        // menu must not open for it. Enter auto-closes the dialog if the guard ever regresses,
        // making the assertion fail instead of the test hanging on the modal loop.
        using TempFile tempFile = TempFile.ReservePath(".jsonl");
        string filePath = tempFile.Path;
        File.WriteAllText(filePath, "{\"id\":1}\n{\"id\":2}");
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

    [Fact]
    public async Task HandleActionMenu_WhenJsonArrayRangeNodeSelected_ReturnsFalse()
    {
        // Arrange — same regression guard as the JSON Lines case, on the JSON Array tree view.
        using TempFile tempFile = TempFile.ReservePath(".json");
        string filePath = tempFile.Path;
        File.WriteAllText(filePath, "[1,2]");
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
}

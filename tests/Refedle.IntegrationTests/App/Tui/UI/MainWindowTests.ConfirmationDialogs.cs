using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Refedle.IntegrationTests.App.Tui.UI;

public sealed partial class MainWindowTests
{
    // Bundles what the Q/C key tests drive on the live pump: the state they arrange and
    // assert on, and the MainWindow the pump runs (also the baseline for modal detection).
    // Disposing releases the state; the pump's own disposal releases the window.
    private sealed record MainWindowContext(AppState State, MainWindow Window) : IDisposable
    {
        public void Dispose()
        {
            State.Dispose();
        }
    }

    /// <summary>
    /// Records that the quit-confirmation modal appeared (into <paramref name="onShown"/>),
    /// then dismisses it with Enter. Same one-shot semantics and pre-modal guard as
    /// <see cref="ModalKeys.SendOnceOnTop"/>.
    /// </summary>
    private static void ObserveQuitConfirmation(IApplication app, View runWindow, Action onShown)
    {
        void OnIteration(object? sender, EventArgs<IApplication?> args)
        {
            // Iterations also run before the modal opens; recording or sending there would
            // waste the one shot before the confirmation is even shown.
            if (app.TopRunnableView is not { } topView || ReferenceEquals(topView, runWindow))
            {
                return;
            }

            onShown();
            app.Iteration -= OnIteration;
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);
        }

        app.Iteration += OnIteration;
    }

    [Fact]
    public async Task KeyDown_WithQKey_WhenUnsavedChanges_HandlesKey()
    {
        // Arrange
        await using LivePumpTestSession<MainWindowContext> session = await LivePumpTestSession.StartAsync(app =>
        {
            AppState state = new();
            state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
            MainWindow mainWindow = new(app, state);
            // Auto-dismiss the "unsaved changes" confirmation dialog by pressing Enter (= "Yes").
            ModalKeys.SendOnceOnTop(app, mainWindow, Key.Enter);
            return (mainWindow, new MainWindowContext(state, mainWindow));
        });

        // Act
        bool handled = await session.InvokeAsync((app, ctx) =>
        {
            ctx.Window.SubscribeKeyHandler();
            ctx.Window.SetFocus();
            return Task.FromResult(app.Keyboard.RaiseKeyDownEvent((Key)'q'));
        });

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public async Task KeyDown_WithQKey_WhenFocusedTableDrillDownIsDirty_ShowsQuitConfirmation()
    {
        // Arrange — FocusedTable with an empty but unsaved DrillDown session (e.g. actions were
        // applied then cleared): the flag, not the stack count, must drive the confirmation
        bool confirmationShown = false;
        await using LivePumpTestSession<MainWindowContext> session = await LivePumpTestSession.StartAsync(app =>
        {
            TableSchema schema = new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
            AppState state = new();
            DrillDownState drillDown = new(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                KeyPath: [],
                ActionStack: [],
                HasUnsavedChanges: true);
            state.EnterFocusedTable(drillDown);
            MainWindow mainWindow = new(app, state);
            // Record whether a modal (the confirmation) is on top, then dismiss it with Enter (= "Yes").
            // Without the dirty check no modal is ever opened.
            ObserveQuitConfirmation(app, mainWindow, () => confirmationShown = true);
            return (mainWindow, new MainWindowContext(state, mainWindow));
        });

        // Act
        bool handled = await session.InvokeAsync((app, ctx) =>
        {
            ctx.Window.SubscribeKeyHandler();
            ctx.Window.SetFocus();
            return Task.FromResult(app.Keyboard.RaiseKeyDownEvent((Key)'q'));
        });

        // Assert
        handled.Should().BeTrue();
        confirmationShown.Should().BeTrue();
    }

    [Fact]
    public async Task KeyDown_WithQKey_WhenErrorViewReplacedFocusedTableAndBaseRootIsDirty_ShowsQuitConfirmation()
    {
        // Arrange — a dirty root stack after an error view replaced a clean FocusedTable:
        // quitting must consult the root flag
        bool confirmationShown = false;
        await using LivePumpTestSession<MainWindowContext> session = await LivePumpTestSession.StartAsync(app =>
        {
            TableSchema schema = new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
            AppState state = new();
            state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
            DrillDownState drillDown = new(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                KeyPath: [],
                ActionStack: [new RenameColumnAction { OldName = "x", NewName = "y" }],
                HasUnsavedChanges: false);
            state.EnterFocusedTable(drillDown);
            state.EnterPlaceholderMode();
            MainWindow mainWindow = new(app, state);
            // Record whether a modal (the confirmation) is on top, then dismiss it with Enter (= "Yes").
            // Without the dirty check no modal is ever opened.
            ObserveQuitConfirmation(app, mainWindow, () => confirmationShown = true);
            return (mainWindow, new MainWindowContext(state, mainWindow));
        });

        // Act
        bool handled = await session.InvokeAsync((app, ctx) =>
        {
            ctx.Window.SubscribeKeyHandler();
            ctx.Window.SetFocus();
            return Task.FromResult(app.Keyboard.RaiseKeyDownEvent((Key)'q'));
        });

        // Assert
        handled.Should().BeTrue();
        confirmationShown.Should().BeTrue();
    }

    [Fact]
    public async Task KeyDown_WithCKey_WhenFocusedTableDrillDownHasActions_ClearsOnlyDrillDownStack()
    {
        // Arrange — confirming the clear from FocusedTable must only clear the DrillDown's own
        // ActionStack, leaving the base table's ActionStack untouched
        await using LivePumpTestSession<MainWindowContext> session = await LivePumpTestSession.StartAsync(app =>
        {
            TableSchema schema = new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
            AppState state = new();
            state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
            DrillDownState drillDown = new(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                KeyPath: [],
                ActionStack: [new RenameColumnAction { OldName = "drill", NewName = "renamed_drill" }]);
            state.EnterFocusedTable(drillDown);
            MainWindow mainWindow = new(app, state);
            // MessageBox.Query defaults focus to the last button ("No"); move focus to "Yes" (Left)
            // then confirm (Enter). Sent once, on the confirmation's first iteration only.
            ModalKeys.SendOnceOnTop(app, mainWindow, Key.CursorLeft, Key.Enter);
            return (mainWindow, new MainWindowContext(state, mainWindow));
        });

        // Act
        bool handled = await session.InvokeAsync((app, ctx) =>
        {
            ctx.Window.SubscribeKeyHandler();
            ctx.Window.SetFocus();
            return Task.FromResult(app.Keyboard.RaiseKeyDownEvent((Key)'c'));
        });

        // Assert
        (int rootActionCount, DrillDownState? drillDownAfter) = await session.InvokeAsync((_, ctx) =>
            Task.FromResult((ctx.State.ActionStack.Count, ctx.State.GetDrillDownOrNull())));
        handled.Should().BeTrue();
        rootActionCount.Should().Be(1);
        DrillDownState drillDownTyped = drillDownAfter.Should().BeOfType<DrillDownState>().Which;
        drillDownTyped.ActionStack.Should().BeEmpty();
        drillDownTyped.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task KeyDown_WithCKey_WhenErrorViewReplacedFocusedTableAndBaseStackHasActions_ClearsBaseStack()
    {
        // Arrange — the DrillDown carried actions before an error view replaced FocusedTable;
        // confirming the clear must clear the base stack and leave no DrillDown session behind
        await using LivePumpTestSession<MainWindowContext> session = await LivePumpTestSession.StartAsync(app =>
        {
            TableSchema schema = new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
            AppState state = new();
            DrillDownState drillDown = new(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                KeyPath: [],
                ActionStack: [new RenameColumnAction { OldName = "drill", NewName = "renamed_drill" }]);
            state.EnterFocusedTable(drillDown);
            state.EnterPlaceholderMode();
            state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
            MainWindow mainWindow = new(app, state);
            // MessageBox.Query defaults focus to the last button ("No"); move focus to "Yes" (Left)
            // then confirm (Enter). Sent once, on the confirmation's first iteration only.
            ModalKeys.SendOnceOnTop(app, mainWindow, Key.CursorLeft, Key.Enter);
            return (mainWindow, new MainWindowContext(state, mainWindow));
        });

        // Act
        bool handled = await session.InvokeAsync((app, ctx) =>
        {
            ctx.Window.SubscribeKeyHandler();
            ctx.Window.SetFocus();
            return Task.FromResult(app.Keyboard.RaiseKeyDownEvent((Key)'c'));
        });

        // Assert
        (int rootActionCount, bool rootDirty, bool hasDrillDown) = await session.InvokeAsync((app, ctx) => Task.FromResult(
            (ctx.State.ActionStack.Count, ctx.State.HasUnsavedChanges, ctx.State.TryGetDrillDown(out _))));
        handled.Should().BeTrue();
        rootActionCount.Should().Be(0);
        rootDirty.Should().BeTrue();
        hasDrillDown.Should().BeFalse();
    }
}

using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

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
    public void KeyDown_WithOKey_HandlesOpen()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent(Key.O);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithSKey_HandlesSave()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent(Key.S);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQKey_HandlesQuit()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent(Key.Q);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQuestionMarkAndShift_HandlesHelp()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        // Simulate '?' with Shift mask (Shift + /)
        var handled = app.Keyboard.RaiseKeyDownEvent((KeyCode)'?' | KeyCode.ShiftMask);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithTKey_WhenNoFileLoaded_ReturnsFalse()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent(Key.T);

        // Assert
        handled.Should().BeFalse();
    }

    [Fact]
    public void KeyDown_WithXKey_WhenNoFileLoaded_ReturnsFalse()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent(Key.X);

        // Assert
        handled.Should().BeFalse();
    }

    [Fact]
    public void KeyDown_WithUnrecognizedKey_ReturnsFalse()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent(Key.F12);

        // Assert
        handled.Should().BeFalse();
    }

    [Fact]
    public void KeyDown_WithHelpKey_ShowsHelpDialog()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        // '?' has implicit cast from char to Key in Terminal.Gui v2
        var handled = app.Keyboard.RaiseKeyDownEvent((Key)'?');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithOKey_WhenInputFocused_DoesNotTriggerFileOpen()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        mainWindow.CanFocus = true;
        using var textField = new TextField { CanFocus = true };
        mainWindow.Add(textField);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.Visible = true;
        mainWindow.CanFocus = true;
        textField.CanFocus = true;
        mainWindow.SubscribeKeyHandler();
        app.LayoutAndDraw();
        textField.SetFocus();
        app.LayoutAndDraw();

        var handled = app.Keyboard.RaiseKeyDownEvent(Key.O);

        // Assert
        // Global shortcut 'o' should be ignored by AppKeyHandler,
        // but the key event should be handled by TextField itself (returning true).
        handled.Should().BeTrue();
    }

    [Fact]
    public async Task KeyDown_WithQKey_WhenUnsavedChanges_HandlesKey()
    {
        // Arrange
        await using var session = await LivePumpTestSession.StartAsync(app =>
        {
            var state = new AppState();
            state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
            var mainWindow = new MainWindow(app, state);
            // Auto-dismiss the "unsaved changes" confirmation dialog by pressing Enter (= "Yes").
            ModalKeys.SendOnceOnTop(app, mainWindow, Key.Enter);
            return (mainWindow, new MainWindowContext(state, mainWindow));
        });

        // Act
        var handled = await session.InvokeAsync((app, ctx) =>
        {
            ctx.Window.SubscribeKeyHandler();
            ctx.Window.SetFocus();
            return Task.FromResult(app.Keyboard.RaiseKeyDownEvent((Key)'q'));
        });

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQKey_WhenFocusedTableDrillDownActionStackEmpty_QuitsWithoutConfirmation()
    {
        // Arrange — base ActionStack has entries, but the active DrillDown's own stack is empty;
        // quitting from FocusedTable must consult the DrillDown's stack, not the base one
        using var app = TestAppFactory.Create();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
        var drillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonLinesTree, KeyPath: [], ActionStack: []);
        state.EnterFocusedTable(drillDown);
        using var mainWindow = new MainWindow(app, state);
        // No dialog auto-dismiss wired: if the wrong stack were consulted, this would hang.
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQKey_WhenErrorViewReplacedFocusedTableAndBaseActionStackEmpty_QuitsWithoutConfirmation()
    {
        // Arrange — the DrillDown carried actions before an error view replaced FocusedTable;
        // quitting must consult the (empty) base stack, not those actions
        using var app = TestAppFactory.Create();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState();
        var drillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            schema,
            ViewMode.JsonLinesTree,
            KeyPath: [],
            ActionStack: [new RenameColumnAction { OldName = "x", NewName = "y" }]);
        state.EnterFocusedTable(drillDown);
        state.EnterPlaceholderMode();
        using var mainWindow = new MainWindow(app, state);
        // No dialog auto-dismiss wired: if the wrong stack were consulted, this would hang.
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQKey_AfterRootRecipeSaved_QuitsWithoutConfirmation()
    {
        // Arrange — the issue #340 scenario: a non-empty ActionStack that was just saved to a
        // recipe; the count-based check would prompt, the unsaved-changes flag must not
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
        state.MarkRecipeSaved();
        using var mainWindow = new MainWindow(app, state);
        // No dialog auto-dismiss wired: if the flag were ignored, this would hang.
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQKey_AfterDrillDownRecipeSaved_QuitsWithoutConfirmation()
    {
        // Arrange — FocusedTable with a saved (clean) DrillDown stack while the root stack is
        // dirty: quitting must consult the current mode's flag, neither the stack counts nor the
        // root flag
        using var app = TestAppFactory.Create();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
        var drillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            schema,
            ViewMode.JsonLinesTree,
            KeyPath: [],
            ActionStack: [new RenameColumnAction { OldName = "drill", NewName = "renamed_drill" }],
            HasUnsavedChanges: false);
        state.EnterFocusedTable(drillDown);
        using var mainWindow = new MainWindow(app, state);
        // No dialog auto-dismiss wired: if the wrong flag were consulted, this would hang.
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public async Task KeyDown_WithQKey_WhenFocusedTableDrillDownIsDirty_ShowsQuitConfirmation()
    {
        // Arrange — FocusedTable with an empty but unsaved DrillDown session (e.g. actions were
        // applied then cleared): the flag, not the stack count, must drive the confirmation
        var confirmationShown = false;
        await using var session = await LivePumpTestSession.StartAsync(app =>
        {
            var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
            var state = new AppState();
            var drillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                KeyPath: [],
                ActionStack: [],
                HasUnsavedChanges: true);
            state.EnterFocusedTable(drillDown);
            var mainWindow = new MainWindow(app, state);
            // Record whether a modal (the confirmation) is on top, then dismiss it with Enter (= "Yes").
            // Without the dirty check no modal is ever opened.
            ObserveQuitConfirmation(app, mainWindow, () => confirmationShown = true);
            return (mainWindow, new MainWindowContext(state, mainWindow));
        });

        // Act
        var handled = await session.InvokeAsync((app, ctx) =>
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
        var confirmationShown = false;
        await using var session = await LivePumpTestSession.StartAsync(app =>
        {
            var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
            var state = new AppState();
            state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
            var drillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                KeyPath: [],
                ActionStack: [new RenameColumnAction { OldName = "x", NewName = "y" }],
                HasUnsavedChanges: false);
            state.EnterFocusedTable(drillDown);
            state.EnterPlaceholderMode();
            var mainWindow = new MainWindow(app, state);
            // Record whether a modal (the confirmation) is on top, then dismiss it with Enter (= "Yes").
            // Without the dirty check no modal is ever opened.
            ObserveQuitConfirmation(app, mainWindow, () => confirmationShown = true);
            return (mainWindow, new MainWindowContext(state, mainWindow));
        });

        // Act
        var handled = await session.InvokeAsync((app, ctx) =>
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
        await using var session = await LivePumpTestSession.StartAsync(app =>
        {
            var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
            var state = new AppState();
            state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
            var drillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                KeyPath: [],
                ActionStack: [new RenameColumnAction { OldName = "drill", NewName = "renamed_drill" }]);
            state.EnterFocusedTable(drillDown);
            var mainWindow = new MainWindow(app, state);
            // MessageBox.Query defaults focus to the last button ("No"); move focus to "Yes" (Left)
            // then confirm (Enter). Sent once, on the confirmation's first iteration only.
            ModalKeys.SendOnceOnTop(app, mainWindow, Key.CursorLeft, Key.Enter);
            return (mainWindow, new MainWindowContext(state, mainWindow));
        });

        // Act
        var handled = await session.InvokeAsync((app, ctx) =>
        {
            ctx.Window.SubscribeKeyHandler();
            ctx.Window.SetFocus();
            return Task.FromResult(app.Keyboard.RaiseKeyDownEvent((Key)'c'));
        });

        // Assert
        var (rootActionCount, drillDownAfter) = await session.InvokeAsync((_, ctx) =>
            Task.FromResult((ctx.State.ActionStack.Count, ctx.State.GetDrillDownOrNull())));
        handled.Should().BeTrue();
        rootActionCount.Should().Be(1);
        var drillDownTyped = drillDownAfter.Should().BeOfType<DrillDownState>().Which;
        drillDownTyped.ActionStack.Should().BeEmpty();
        drillDownTyped.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task KeyDown_WithCKey_WhenErrorViewReplacedFocusedTableAndBaseStackHasActions_ClearsBaseStack()
    {
        // Arrange — the DrillDown carried actions before an error view replaced FocusedTable;
        // confirming the clear must clear the base stack and leave no DrillDown session behind
        await using var session = await LivePumpTestSession.StartAsync(app =>
        {
            var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
            var state = new AppState();
            var drillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                KeyPath: [],
                ActionStack: [new RenameColumnAction { OldName = "drill", NewName = "renamed_drill" }]);
            state.EnterFocusedTable(drillDown);
            state.EnterPlaceholderMode();
            state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
            var mainWindow = new MainWindow(app, state);
            // MessageBox.Query defaults focus to the last button ("No"); move focus to "Yes" (Left)
            // then confirm (Enter). Sent once, on the confirmation's first iteration only.
            ModalKeys.SendOnceOnTop(app, mainWindow, Key.CursorLeft, Key.Enter);
            return (mainWindow, new MainWindowContext(state, mainWindow));
        });

        // Act
        var handled = await session.InvokeAsync((app, ctx) =>
        {
            ctx.Window.SubscribeKeyHandler();
            ctx.Window.SetFocus();
            return Task.FromResult(app.Keyboard.RaiseKeyDownEvent((Key)'c'));
        });

        // Assert
        var (rootActionCount, rootDirty, hasDrillDown) = await session.InvokeAsync((app, ctx) => Task.FromResult(
            (ctx.State.ActionStack.Count, ctx.State.HasUnsavedChanges, ctx.State.TryGetDrillDown(out _))));
        handled.Should().BeTrue();
        rootActionCount.Should().Be(0);
        rootDirty.Should().BeTrue();
        hasDrillDown.Should().BeFalse();
    }
}

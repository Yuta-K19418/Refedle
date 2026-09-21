using AwesomeAssertions;
using Refedle.App;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.Tests.App;

public sealed partial class MainWindowTests
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
    public void KeyDown_WithOKey_HandlesOpen()
    {
        // Arrange
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
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
    public void KeyDown_WithQKey_WhenUnsavedChanges_HandlesKey()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
        using var mainWindow = new MainWindow(app, state);
        // Auto-dismiss the "unsaved changes" confirmation dialog by pressing Enter (= "Yes").
        app.Iteration += (_, _) => app.Keyboard.RaiseKeyDownEvent(Key.Enter);
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
    public void KeyDown_WithQKey_WhenFocusedTableDrillDownActionStackEmpty_QuitsWithoutConfirmation()
    {
        // Arrange — base ActionStack has entries, but the active DrillDown's own stack is empty;
        // quitting from FocusedTable must consult the DrillDown's stack, not the base one
        using var app = CreateTestApp();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState { CurrentMode = ViewMode.FocusedTable };
        state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
        state.DrillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonLinesTree, KeyPath: [], ActionStack: []);
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
    public void KeyDown_WithQKey_WhenBaseActionStackEmptyAndStaleDrillDownHasActions_QuitsWithoutConfirmation()
    {
        // Arrange — a stale DrillDown (left over from Backspace navigation) carries actions, but the
        // current view is the base table; quitting must consult the base stack, not the stale one
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
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState { CurrentMode = ViewMode.FocusedTable };
        state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
        state.DrillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            schema,
            ViewMode.JsonLinesTree,
            KeyPath: [],
            ActionStack: [new RenameColumnAction { OldName = "drill", NewName = "renamed_drill" }],
            HasUnsavedChanges: false);
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
    public void KeyDown_WithQKey_WhenFocusedTableDrillDownIsDirty_ShowsQuitConfirmation()
    {
        // Arrange — FocusedTable with an empty but unsaved DrillDown session (e.g. actions were
        // applied then cleared): the flag, not the stack count, must drive the confirmation
        using var app = CreateTestApp();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState { CurrentMode = ViewMode.FocusedTable };
        state.DrillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            schema,
            ViewMode.JsonLinesTree,
            KeyPath: [],
            ActionStack: [],
            HasUnsavedChanges: true);
        using var mainWindow = new MainWindow(app, state);
        // Record whether a modal (the confirmation) is on top, then dismiss it with Enter (= "Yes").
        // Without the dirty check no modal is ever opened.
        var confirmationShown = false;
        app.Iteration += (_, _) =>
        {
            confirmationShown |= app.TopRunnableView is { } topView && !ReferenceEquals(topView, mainWindow);
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);
        };
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
        confirmationShown.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQKey_WhenBaseRootIsDirtyAndStaleDrillDownIsClean_ShowsQuitConfirmation()
    {
        // Arrange — base table with a dirty root stack and a clean stale DrillDown (left over from
        // Backspace navigation): quitting must consult the root flag
        using var app = CreateTestApp();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
        state.DrillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            schema,
            ViewMode.JsonLinesTree,
            KeyPath: [],
            ActionStack: [new RenameColumnAction { OldName = "x", NewName = "y" }],
            HasUnsavedChanges: false);
        using var mainWindow = new MainWindow(app, state);
        // Record whether a modal (the confirmation) is on top, then dismiss it with Enter (= "Yes").
        // Without the dirty check no modal is ever opened.
        var confirmationShown = false;
        app.Iteration += (_, _) =>
        {
            confirmationShown |= app.TopRunnableView is { } topView && !ReferenceEquals(topView, mainWindow);
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);
        };
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
        confirmationShown.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithCKey_WhenFocusedTableDrillDownHasActions_ClearsOnlyDrillDownStack()
    {
        // Arrange — confirming the clear from FocusedTable must only clear the DrillDown's own
        // ActionStack, leaving the base table's ActionStack untouched
        using var app = CreateTestApp();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState { CurrentMode = ViewMode.FocusedTable };
        state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
        state.DrillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            schema,
            ViewMode.JsonLinesTree,
            KeyPath: [],
            ActionStack: [new RenameColumnAction { OldName = "drill", NewName = "renamed_drill" }]);
        using var mainWindow = new MainWindow(app, state);
        // MessageBox.Query defaults focus to the last button ("No"); move focus to "Yes" (Left)
        // then confirm (Enter). Self-unsubscribes so it fires only once — resending on every
        // subsequent Iteration tick would keep steering focus in the (now dialog-free) main window.
        void ConfirmYes(object? sender, EventArgs e)
        {
            app.Iteration -= ConfirmYes;
            app.Keyboard.RaiseKeyDownEvent(Key.CursorLeft);
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);
        }

        app.Iteration += ConfirmYes;
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent((Key)'c');

        // Assert
        handled.Should().BeTrue();
        state.ActionStack.Should().ContainSingle();
        var drillDown = state.DrillDown.Should().BeOfType<DrillDownState>().Which;
        drillDown.ActionStack.Should().BeEmpty();
        drillDown.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithCKey_WhenBaseStackHasActions_ClearsOnlyBaseStack()
    {
        // Arrange — a stale DrillDown (left over from Backspace navigation) carries actions, but
        // the current view is the base table; confirming the clear must only clear the base stack
        using var app = CreateTestApp();
        var schema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using var state = new AppState
        {
            DrillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                schema,
                ViewMode.JsonLinesTree,
                KeyPath: [],
                ActionStack: [new RenameColumnAction { OldName = "drill", NewName = "renamed_drill" }]),
        };
        state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
        using var mainWindow = new MainWindow(app, state);
        // MessageBox.Query defaults focus to the last button ("No"); move focus to "Yes" (Left)
        // then confirm (Enter). Self-unsubscribes so it fires only once — resending on every
        // subsequent Iteration tick would keep steering focus in the (now dialog-free) main window.
        void ConfirmYes(object? sender, EventArgs e)
        {
            app.Iteration -= ConfirmYes;
            app.Keyboard.RaiseKeyDownEvent(Key.CursorLeft);
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);
        }

        app.Iteration += ConfirmYes;
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        var handled = app.Keyboard.RaiseKeyDownEvent((Key)'c');

        // Assert
        handled.Should().BeTrue();
        state.ActionStack.Should().BeEmpty();
        state.HasUnsavedChanges.Should().BeTrue();
        var drillDown = state.DrillDown.Should().BeOfType<DrillDownState>().Which;
        drillDown.ActionStack.Should().ContainSingle();
    }
}

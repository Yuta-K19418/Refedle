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
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

public sealed partial class MainWindowTests
{
    [Fact]
    public void KeyDown_WithOKey_HandlesOpen()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using MainWindow mainWindow = new(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        bool handled = app.Keyboard.RaiseKeyDownEvent(Key.O);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithSKey_HandlesSave()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using MainWindow mainWindow = new(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        bool handled = app.Keyboard.RaiseKeyDownEvent(Key.S);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQKey_HandlesQuit()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using MainWindow mainWindow = new(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        bool handled = app.Keyboard.RaiseKeyDownEvent(Key.Q);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQuestionMarkAndShift_HandlesHelp()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using MainWindow mainWindow = new(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        // Simulate '?' with Shift mask (Shift + /)
        bool handled = app.Keyboard.RaiseKeyDownEvent((KeyCode)'?' | KeyCode.ShiftMask);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithTKey_WhenNoFileLoaded_ReturnsFalse()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using MainWindow mainWindow = new(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        bool handled = app.Keyboard.RaiseKeyDownEvent(Key.T);

        // Assert
        handled.Should().BeFalse();
    }

    [Fact]
    public void KeyDown_WithXKey_WhenNoFileLoaded_ReturnsFalse()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using MainWindow mainWindow = new(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        bool handled = app.Keyboard.RaiseKeyDownEvent(Key.X);

        // Assert
        handled.Should().BeFalse();
    }

    [Fact]
    public void KeyDown_WithUnrecognizedKey_ReturnsFalse()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using MainWindow mainWindow = new(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        bool handled = app.Keyboard.RaiseKeyDownEvent(Key.F12);

        // Assert
        handled.Should().BeFalse();
    }

    [Fact]
    public void KeyDown_WithHelpKey_ShowsHelpDialog()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using MainWindow mainWindow = new(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        // '?' has implicit cast from char to Key in Terminal.Gui v2
        bool handled = app.Keyboard.RaiseKeyDownEvent((Key)'?');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithOKey_WhenInputFocused_DoesNotTriggerFileOpen()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using MainWindow mainWindow = new(app, state);
        mainWindow.CanFocus = true;
        using TextField textField = new() { CanFocus = true };
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

        bool handled = app.Keyboard.RaiseKeyDownEvent(Key.O);

        // Assert
        // Global shortcut 'o' should be ignored by AppKeyHandler,
        // but the key event should be handled by TextField itself (returning true).
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQKey_WhenFocusedTableDrillDownActionStackEmpty_QuitsWithoutConfirmation()
    {
        // Arrange — base ActionStack has entries, but the active DrillDown's own stack is empty;
        // quitting from FocusedTable must consult the DrillDown's stack, not the base one
        using IApplication app = TestAppFactory.Create();
        TableSchema schema = new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using AppState state = new();
        state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
        DrillDownState drillDown = new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")], schema, ViewMode.JsonLinesTree, KeyPath: [], ActionStack: []);
        state.EnterFocusedTable(drillDown);
        using MainWindow mainWindow = new(app, state);
        // No dialog auto-dismiss wired: if the wrong stack were consulted, this would hang.
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        bool handled = app.Keyboard.RaiseKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQKey_WhenErrorViewReplacedFocusedTableAndBaseActionStackEmpty_QuitsWithoutConfirmation()
    {
        // Arrange — the DrillDown carried actions before an error view replaced FocusedTable;
        // quitting must consult the (empty) base stack, not those actions
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
        using MainWindow mainWindow = new(app, state);
        // No dialog auto-dismiss wired: if the wrong stack were consulted, this would hang.
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        bool handled = app.Keyboard.RaiseKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQKey_AfterRootRecipeSaved_QuitsWithoutConfirmation()
    {
        // Arrange — the issue #340 scenario: a non-empty ActionStack that was just saved to a
        // recipe; the count-based check would prompt, the unsaved-changes flag must not
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.AddMorphAction(new RenameColumnAction { OldName = "col1", NewName = "new_col1" });
        state.MarkRecipeSaved();
        using MainWindow mainWindow = new(app, state);
        // No dialog auto-dismiss wired: if the flag were ignored, this would hang.
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        bool handled = app.Keyboard.RaiseKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void KeyDown_WithQKey_AfterDrillDownRecipeSaved_QuitsWithoutConfirmation()
    {
        // Arrange — FocusedTable with a saved (clean) DrillDown stack while the root stack is
        // dirty: quitting must consult the current mode's flag, neither the stack counts nor the
        // root flag
        using IApplication app = TestAppFactory.Create();
        TableSchema schema = new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        using AppState state = new();
        state.AddMorphAction(new RenameColumnAction { OldName = "base", NewName = "renamed_base" });
        DrillDownState drillDown = new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            schema,
            ViewMode.JsonLinesTree,
            KeyPath: [],
            ActionStack: [new RenameColumnAction { OldName = "drill", NewName = "renamed_drill" }],
            HasUnsavedChanges: false);
        state.EnterFocusedTable(drillDown);
        using MainWindow mainWindow = new(app, state);
        // No dialog auto-dismiss wired: if the wrong flag were consulted, this would hang.
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        mainWindow.SubscribeKeyHandler();
        mainWindow.SetFocus();
        bool handled = app.Keyboard.RaiseKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
    }
}

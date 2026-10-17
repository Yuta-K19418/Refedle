using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.Dialogs;
using Terminal.Gui.Input;

namespace Refedle.Tests.App.Tui.UI.Views.Dialogs;

public sealed class ActionMenuDialogTests
{
    [Fact]
    public void Constructor_WithValidActions_InitializesDialog()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        string? capturedAction = null;
        using var app = TestAppFactory.Create();

        // Act
        using var dialog = new ActionMenuDialog(actions, action => capturedAction = action);

        // Assert
        dialog.Title.Should().Be("Actions");
        capturedAction.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEmptyActions_InitializesDialog()
    {
        // Arrange
        string[] actions = [];
        string? capturedAction = null;
        using var app = TestAppFactory.Create();

        // Act
        using var dialog = new ActionMenuDialog(actions, action => capturedAction = action);

        // Assert
        dialog.Title.Should().Be("Actions");
        capturedAction.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithSingleAction_InitializesDialog()
    {
        // Arrange
        string[] actions = ["Rename"];
        string? capturedAction = null;
        using var app = TestAppFactory.Create();

        // Act
        using var dialog = new ActionMenuDialog(actions, action => capturedAction = action);

        // Assert
        dialog.Title.Should().Be("Actions");
        capturedAction.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullActions_ThrowsArgumentException()
    {
        // Arrange
        string[]? actions = null;
        string? capturedAction = null;
        using var app = TestAppFactory.Create();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new ActionMenuDialog(actions!, action => capturedAction = action));

        // Assert
        exception.ParamName.Should().Be("availableActions");
    }

    [Fact]
    public void Constructor_WithNullOnConfirmed_ThrowsArgumentException()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        using var app = TestAppFactory.Create();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new ActionMenuDialog(actions, null!));

        // Assert
        exception.ParamName.Should().Be("onConfirmed");
    }

    [Fact]
    public async Task Constructor_WhenUserConfirms_CallsOnConfirmedWithSelectedAction()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        string? capturedAction = null;
        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            var dialog = new ActionMenuDialog(actions, action => capturedAction = action);
            ModalKeys.SendOnceOnTop(app, window, Key.Enter);
            return dialog;
        });

        // Act
        await session.InvokeAsync((app, dialog) =>
        {
            app.Run(dialog);
            return Task.CompletedTask;
        });

        // Assert
        capturedAction.Should().Be("Rename");
    }

    [Fact]
    public void Constructor_WhenUserCancels_DoesNotCallOnConfirmed()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        var callbackCalled = false;
        using var app = TestAppFactory.Create();
        using var dialog = new ActionMenuDialog(actions, _ => callbackCalled = true);
        app.Begin(dialog);
        dialog.NewKeyDownEvent(Key.Esc);

        // Act
        // Already cancelled by NewKeyDownEvent above

        // Assert
        callbackCalled.Should().BeFalse();
    }

    [Fact]
    public void HandleMenuNavigation_WithEscKey_ClosesDialogWithoutConfirm()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        var callbackCalled = false;
        using var app = TestAppFactory.Create();
        using var dialog = new ActionMenuDialog(actions, _ => callbackCalled = true);

        // Act
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent(Key.Esc);

        // Assert
        handled.Should().BeTrue();
        callbackCalled.Should().BeFalse();
    }

    [Fact]
    public void HandleMenuNavigation_WithXKey_ClosesDialogWithoutConfirm()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        var callbackCalled = false;
        using var app = TestAppFactory.Create();
        using var dialog = new ActionMenuDialog(actions, _ => callbackCalled = true);

        // Act
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent(Key.X);

        // Assert
        handled.Should().BeTrue();
        callbackCalled.Should().BeFalse();
    }

    [Fact]
    public void HandleMenuNavigation_WithJKey_MovesSelectionDown()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        var callbackCalled = false;
        using var app = TestAppFactory.Create();
        using var dialog = new ActionMenuDialog(actions, _ => callbackCalled = true);

        // Act
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent(Key.J);

        // Assert
        handled.Should().BeTrue();
        dialog.SelectedItemIndex.Should().Be(1);
        callbackCalled.Should().BeFalse();
    }

    [Fact]
    public void HandleMenuNavigation_WithKKey_MovesSelectionUp()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        var callbackCalled = false;
        using var app = TestAppFactory.Create();
        using var dialog = new ActionMenuDialog(actions, _ => callbackCalled = true);
        app.Begin(dialog);
        dialog.NewKeyDownEvent(Key.J);

        // Act
        var handled = dialog.NewKeyDownEvent(Key.K);

        // Assert
        handled.Should().BeTrue();
        dialog.SelectedItemIndex.Should().Be(0);
        callbackCalled.Should().BeFalse();
    }

    [Fact]
    public void HandleMenuNavigation_WithKKey_AtFirstItem_StaysAtFirst()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        var callbackCalled = false;
        using var app = TestAppFactory.Create();
        using var dialog = new ActionMenuDialog(actions, _ => callbackCalled = true);

        // Act
        app.Begin(dialog);
        dialog.NewKeyDownEvent(Key.K);

        // Assert
        dialog.SelectedItemIndex.Should().Be(0);
        callbackCalled.Should().BeFalse();
    }
}

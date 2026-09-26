using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views.Dialogs;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace Refedle.Tests.App.Tui.Ui.Views.Dialogs;

public sealed class ActionMenuDialogTests
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
    public void Constructor_WithValidActions_InitializesDialog()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        string? capturedAction = null;
        using var app = CreateTestApp();

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
        using var app = CreateTestApp();

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
        using var app = CreateTestApp();

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
        using var app = CreateTestApp();

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
        using var app = CreateTestApp();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new ActionMenuDialog(actions, null!));

        // Assert
        exception.ParamName.Should().Be("onConfirmed");
    }

    [Fact]
    public void Constructor_WhenUserConfirms_CallsOnConfirmedWithSelectedAction()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        string? capturedAction = null;
        using var app = CreateTestApp();
        using var dialog = new ActionMenuDialog(actions, action => capturedAction = action);
        app.Iteration += (_, _) => app.Keyboard.RaiseKeyDownEvent(Key.Enter);

        // Act
        app.Run(dialog);

        // Assert
        capturedAction.Should().Be("Rename");
    }

    [Fact]
    public void Constructor_WhenUserCancels_DoesNotCallOnConfirmed()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        var callbackCalled = false;
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
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
        using var app = CreateTestApp();
        using var dialog = new ActionMenuDialog(actions, _ => callbackCalled = true);

        // Act
        app.Begin(dialog);
        dialog.NewKeyDownEvent(Key.K);

        // Assert
        dialog.SelectedItemIndex.Should().Be(0);
        callbackCalled.Should().BeFalse();
    }
}

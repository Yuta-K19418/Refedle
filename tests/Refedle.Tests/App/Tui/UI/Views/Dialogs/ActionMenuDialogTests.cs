using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.Dialogs;
using Terminal.Gui.App;
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
        using IApplication app = TestAppFactory.Create();

        // Act
        using ActionMenuDialog dialog = new(actions, action => capturedAction = action);

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
        using IApplication app = TestAppFactory.Create();

        // Act
        using ActionMenuDialog dialog = new(actions, action => capturedAction = action);

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
        using IApplication app = TestAppFactory.Create();

        // Act
        using ActionMenuDialog dialog = new(actions, action => capturedAction = action);

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
        using IApplication app = TestAppFactory.Create();

        // Act
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new ActionMenuDialog(actions!, action => capturedAction = action));

        // Assert
        exception.ParamName.Should().Be("availableActions");
    }

    [Fact]
    public void Constructor_WithNullOnConfirmed_ThrowsArgumentException()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        using IApplication app = TestAppFactory.Create();

        // Act
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new ActionMenuDialog(actions, null!));

        // Assert
        exception.ParamName.Should().Be("onConfirmed");
    }

    [Fact]
    public void Constructor_WhenUserCancels_DoesNotCallOnConfirmed()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        bool callbackCalled = false;
        using IApplication app = TestAppFactory.Create();
        using ActionMenuDialog dialog = new(actions, _ => callbackCalled = true);
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
        bool callbackCalled = false;
        using IApplication app = TestAppFactory.Create();
        using ActionMenuDialog dialog = new(actions, _ => callbackCalled = true);

        // Act
        app.Begin(dialog);
        bool handled = dialog.NewKeyDownEvent(Key.Esc);

        // Assert
        handled.Should().BeTrue();
        callbackCalled.Should().BeFalse();
    }

    [Fact]
    public void HandleMenuNavigation_WithXKey_ClosesDialogWithoutConfirm()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        bool callbackCalled = false;
        using IApplication app = TestAppFactory.Create();
        using ActionMenuDialog dialog = new(actions, _ => callbackCalled = true);

        // Act
        app.Begin(dialog);
        bool handled = dialog.NewKeyDownEvent(Key.X);

        // Assert
        handled.Should().BeTrue();
        callbackCalled.Should().BeFalse();
    }

    [Fact]
    public void HandleMenuNavigation_WithJKey_MovesSelectionDown()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        bool callbackCalled = false;
        using IApplication app = TestAppFactory.Create();
        using ActionMenuDialog dialog = new(actions, _ => callbackCalled = true);

        // Act
        app.Begin(dialog);
        bool handled = dialog.NewKeyDownEvent(Key.J);

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
        bool callbackCalled = false;
        using IApplication app = TestAppFactory.Create();
        using ActionMenuDialog dialog = new(actions, _ => callbackCalled = true);
        app.Begin(dialog);
        dialog.NewKeyDownEvent(Key.J);

        // Act
        bool handled = dialog.NewKeyDownEvent(Key.K);

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
        bool callbackCalled = false;
        using IApplication app = TestAppFactory.Create();
        using ActionMenuDialog dialog = new(actions, _ => callbackCalled = true);

        // Act
        app.Begin(dialog);
        dialog.NewKeyDownEvent(Key.K);

        // Assert
        dialog.SelectedItemIndex.Should().Be(0);
        callbackCalled.Should().BeFalse();
    }
}

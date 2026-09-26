using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views.Dialogs;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.Ui.Views.Dialogs;

public sealed class HelpDialogTests
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
    public void OnKeyDown_WithEscKey_ReturnsTrue()
    {
        // Arrange
        using var app = CreateTestApp();
        using var dialog = new HelpDialog();

        // Act
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent(Key.Esc);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_WithQKey_ReturnsTrue()
    {
        // Arrange
        using var app = CreateTestApp();
        using var dialog = new HelpDialog();

        // Act
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent(Key.Q);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_WithLowercaseQKey_ReturnsTrue()
    {
        // Arrange
        using var app = CreateTestApp();
        using var dialog = new HelpDialog();

        // Act
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_WithQuestionMarkKey_ReturnsTrue()
    {
        // Arrange
        using var app = CreateTestApp();
        using var dialog = new HelpDialog();

        // Act
        // '?' has implicit cast from char to Key in Terminal.Gui v2
        app.Begin(dialog);
        var handled = dialog.NewKeyDownEvent((Key)'?');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_InitializesDialogCorrectly()
    {
        // Arrange
        using var app = CreateTestApp();

        // Act
        using var dialog = new HelpDialog();

        // Assert
        dialog.Title.Should().Be("Help - Key Bindings");
    }

    [Fact]
    public void Constructor_HelpText_IncludesBackspaceBinding()
    {
        // Arrange
        using var app = CreateTestApp();

        // Act
        using var dialog = new HelpDialog();

        // Assert
        var label = dialog.SubViews.OfType<Label>().Single();
        label.Text.Should().Contain("BackSpace");
    }

    [Fact]
    public void OnKeyDown_WithOtherKey_DoesNotCloseDialog()
    {
        // Arrange
        using var app = CreateTestApp();
        using var dialog = new HelpDialog();

        // Act
        app.Begin(dialog);
        _ = dialog.NewKeyDownEvent(Key.A);

        // Assert
        // Verify that the dialog remains as the TopRunnableView
        app.TopRunnableView.Should().Be(dialog);
    }
}

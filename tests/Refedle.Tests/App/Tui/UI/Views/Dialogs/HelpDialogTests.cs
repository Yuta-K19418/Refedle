using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.Dialogs;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views.Dialogs;

public sealed class HelpDialogTests
{
    [Fact]
    public void OnKeyDown_WithEscKey_ReturnsTrue()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using HelpDialog dialog = new();

        // Act
        app.Begin(dialog);
        bool handled = dialog.NewKeyDownEvent(Key.Esc);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_WithQKey_ReturnsTrue()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using HelpDialog dialog = new();

        // Act
        app.Begin(dialog);
        bool handled = dialog.NewKeyDownEvent(Key.Q);

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_WithLowercaseQKey_ReturnsTrue()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using HelpDialog dialog = new();

        // Act
        app.Begin(dialog);
        bool handled = dialog.NewKeyDownEvent((Key)'q');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_WithQuestionMarkKey_ReturnsTrue()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using HelpDialog dialog = new();

        // Act
        // '?' has implicit cast from char to Key in Terminal.Gui v2
        app.Begin(dialog);
        bool handled = dialog.NewKeyDownEvent((Key)'?');

        // Assert
        handled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_InitializesDialogCorrectly()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();

        // Act
        using HelpDialog dialog = new();

        // Assert
        dialog.Title.Should().Be("Help - Key Bindings");
    }

    [Fact]
    public void Constructor_HelpText_IncludesBackspaceBinding()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();

        // Act
        using HelpDialog dialog = new();

        // Assert
        Label label = dialog.SubViews.OfType<Label>().Single();
        label.Text.Should().Contain("BackSpace");
    }

    [Fact]
    public void OnKeyDown_WithOtherKey_DoesNotCloseDialog()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using HelpDialog dialog = new();

        // Act
        app.Begin(dialog);
        _ = dialog.NewKeyDownEvent(Key.A);

        // Assert
        // Verify that the dialog remains as the TopRunnableView
        app.TopRunnableView.Should().Be(dialog);
    }
}

using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

public sealed partial class MainWindowTests
{
    [Fact]
    public void Constructor_WithNoFileOpen_ShowsFileMenuAndEmptyPathBarAfterItOnFirstRow()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using MainWindow mainWindow = new(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        app.LayoutAndDraw();

        // Assert
        MenuBar menuBar = mainWindow.SubViews.OfType<MenuBar>().Should().ContainSingle().Which;
        menuBar.SubViews.OfType<MenuBarItem>().Should().ContainSingle().Which.Title.Should().Be("_File");
        FilePathBar pathBar = mainWindow.SubViews.OfType<FilePathBar>().Should().ContainSingle().Which;
        pathBar.Frame.X.Should().Be(7);
        pathBar.Frame.Y.Should().Be(menuBar.Frame.Y);
        pathBar.Text.Should().BeEmpty();
    }
}

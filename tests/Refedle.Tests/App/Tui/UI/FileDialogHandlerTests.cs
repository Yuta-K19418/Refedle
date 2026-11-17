using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.Workers;
using Refedle.Tests.App.Tui.Workers.Schema;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

public sealed class FileDialogHandlerTests
{
    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        Action act = () =>
        {
            _ = new FileDialogHandler(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, _ => { });
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullScannerFactory_ThrowsArgumentNullException()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        Action act = () =>
        {
            _ = new FileDialogHandler(app, state, viewManager, _ => { }, () => { }, null!, _ => { });
        };

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullFilePathChangedCallback_ThrowsArgumentNullException()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        using Window window = new();
        ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        using ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), action => action());

        // Act
        Action act = () =>
        {
            _ = new FileDialogHandler(app, state, viewManager, _ => { }, () => { }, TestSchemaScannerFactories.Csv, null!);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}

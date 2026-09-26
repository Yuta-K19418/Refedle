using Terminal.Gui.App;

namespace Refedle.App.Tui.Ui;

/// <summary>
/// Main Terminal.Gui application shell for Refedle.
/// Manages application lifecycle, window creation, and view orchestration.
/// </summary>
internal static class TuiApplication
{
    /// <summary>
    /// Creates and initializes the TUI application.
    /// </summary>
    /// <returns>A tuple containing the IApplication and MainWindow to be run by the caller.</returns>
    /// <remarks>
    /// The caller is responsible for disposing both the IApplication and MainWindow instances
    /// returned by this method. Child views added to the MainWindow will be disposed automatically
    /// when the MainWindow is disposed.
    /// </remarks>
    public static (IApplication app, MainWindow mainWindow) Create()
    {
        var app = Application.Create();
        var state = new AppState();
        var mainWindow = new MainWindow(app, state);

        return (app, mainWindow);
    }
}

using Terminal.Gui.App;
using Terminal.Gui.Drivers;

namespace Refedle.Tests.App.Tui.UI;

/// <summary>
/// Creates the headless ANSI-driver <see cref="IApplication"/> shared by UI tests, with the
/// driver initialized and the screen fixed at 80x25. Draw-only tests and
/// <see cref="LivePumpTestSession{T}"/> both build on this factory so the initialization
/// cannot drift apart.
/// </summary>
internal static class TestAppFactory
{
    public static IApplication Create()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        Assert.NotNull(app.Driver);
        app.Driver.SetScreenSize(80, 25);
        return app;
    }
}

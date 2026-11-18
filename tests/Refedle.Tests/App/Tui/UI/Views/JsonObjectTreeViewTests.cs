using Terminal.Gui.App;
using Terminal.Gui.Drivers;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed partial class JsonObjectTreeViewTests : IDisposable
{
    private readonly IApplication _app;

    public JsonObjectTreeViewTests()
    {
        _app = Application.Create();
        _app.Init(DriverRegistry.Names.ANSI);
        Assert.NotNull(_app.Driver);
        _app.Driver.SetScreenSize(80, 25);
    }

    public void Dispose() => _app.Dispose();
}

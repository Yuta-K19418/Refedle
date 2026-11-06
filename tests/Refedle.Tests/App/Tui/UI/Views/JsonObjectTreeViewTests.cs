using System.Globalization;
using System.Text;
using Refedle.Engine.IO.JsonObject;
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

    internal static JsonRawBytes ToBytes(string json) =>
        Encoding.UTF8.GetBytes(json);

    internal static List<JsonObjectEntry> CreateNumberedEntries(int count) =>
        [.. Enumerable
            .Range(0, count)
            .Select(i => new JsonObjectEntry(
                string.Create(CultureInfo.InvariantCulture, $"k{i}"),
                ToBytes(i.ToString(CultureInfo.InvariantCulture))))];
}

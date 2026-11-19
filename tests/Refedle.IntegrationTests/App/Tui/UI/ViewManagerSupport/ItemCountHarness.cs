using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui.Workers.Schema;
using Terminal.Gui.Views;
using static Refedle.IntegrationTests.App.Tui.UI.ViewManagerSupport.ViewManagerTestData;

namespace Refedle.IntegrationTests.App.Tui.UI.ViewManagerSupport;

/// <summary>
/// ViewManager whose UI-thread invoke is captured in a <see cref="SteppableCallbackQueue"/>, so scan
/// notifications run only when the test executes them. The optional filter is put on the Action Stack.
/// </summary>
internal sealed class ItemCountHarness : IDisposable
{
    private readonly StatusBar _statusBar = new();

    public ItemCountHarness(
        string filePath,
        DataFormat format,
        IRowIndexer rawIndexer,
        IFilterScanRunner runner,
        FilterAction? filter)
    {
        State.StartNewFile(filePath);
        State.CompleteCsvLoad(rawIndexer, CreateItemCountSchema(format));
        if (filter is not null)
        {
            State.AddMorphAction(filter);
        }

        Window.Add(_statusBar);
        ModeController modeController = new(State, action => action(), TestSchemaScannerFactories.JsonLines);
        ViewManager = new ViewManager(Window, State, modeController, runner, Queue.Enqueue);
    }

    public AppState State { get; } = new();
    public Window Window { get; } = new();
    public SteppableCallbackQueue Queue { get; } = new();
    public ViewManager ViewManager { get; }

    public void Dispose()
    {
        ViewManager.Dispose();
        Window.Dispose();
        _statusBar.Dispose();
        State.Dispose();
    }
}

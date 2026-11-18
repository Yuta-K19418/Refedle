using Refedle.App.Tui.UI.Views;
using Terminal.Gui.Input;

namespace Refedle.Tests.App.Tui.UI.Views;

internal sealed class KeyProcessingMorphTableView : MorphTableView
{
    // Exposes the protected OnKeyDown for characterization testing.
    public bool ProcessKey(Key key) => OnKeyDown(key);
}

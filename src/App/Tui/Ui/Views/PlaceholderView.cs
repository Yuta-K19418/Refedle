using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views;

/// <summary>
/// A placeholder view that displays the loaded file path.
/// This will be replaced by VirtualGridView in the next phase.
/// </summary>
internal sealed class PlaceholderView : View
{
    private PlaceholderView(AppState state)
    {
        X = 0;
        Y = 1; // Below menu bar
        Width = Dim.Fill();
        Height = Dim.Fill() - 1; // Above status bar

        Add(
            new Label
            {
                X = Pos.Center(),
                Y = Pos.Center() - 1,
                Text = $"File loaded: {state.CurrentFilePath}",
            },
            new Label
            {
                X = Pos.Center(),
                Y = Pos.Center() + 1,
                Text = "Press q to quit or o to open another file",
            }
        );
    }

    /// <summary>
    /// Creates a new PlaceholderView instance.
    /// </summary>
    /// <param name="state">The application state containing the current file path.</param>
    /// <returns>A new PlaceholderView.</returns>
    public static PlaceholderView Create(AppState state) => new(state);
}

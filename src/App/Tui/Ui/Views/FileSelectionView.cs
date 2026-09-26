using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views;

/// <summary>
/// A view that displays instructions for file selection.
/// </summary>
internal sealed class FileSelectionView : View
{
    private FileSelectionView()
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
                Text = "Welcome to Refedle",
            },
            new Label
            {
                X = Pos.Center(),
                Y = Pos.Center() + 1,
                Text = "Press o to open a file",
            }
        );
    }

    /// <summary>
    /// Creates a new FileSelectionView instance.
    /// </summary>
    /// <returns>A new FileSelectionView.</returns>
    public static FileSelectionView Create() => new();
}

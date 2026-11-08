using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI.Views;

/// <summary>
/// A centered message view shown while a background operation prepares the next content view.
/// The size and position are decided by the container that swaps this view in.
/// </summary>
internal sealed class LoadingView : View
{
    private LoadingView(string message)
    {
        Add(
            new Label
            {
                X = Pos.Center(),
                Y = Pos.Center(),
                Text = message,
            }
        );
    }

    /// <summary>
    /// Creates a new <see cref="LoadingView"/> showing <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message to display centered in the view.</param>
    /// <returns>A new <see cref="LoadingView"/>.</returns>
    internal static LoadingView Create(string message) => new(message);
}

using Terminal.Gui.ViewBase;

namespace Refedle.App.Tui.UI;

/// <summary>
/// Owns the content container below the breadcrumb bar and the view currently shown in it.
/// </summary>
/// <param name="topAnchor">The view the content container is placed directly beneath.</param>
/// <remarks>
/// Threading contract: all state is UI-thread exclusive. <see cref="Replace"/>, <see cref="Dispose"/>
/// and the members of <see cref="Container"/> must be called on the UI thread.
/// </remarks>
internal sealed class ContentViewHost(View topAnchor) : IDisposable
{
    /// <summary>
    /// Gets the container that hosts the current view.
    /// </summary>
    internal View Container { get; } = new()
    {
        X = 0,
        Y = Pos.Bottom(topAnchor),
        Width = Dim.Fill(),
        Height = Dim.Fill() - 1, // Leave room for StatusBar at the bottom
        CanFocus = true,
    };

    /// <summary>
    /// Gets the view currently shown in the container, or <c>null</c> before the first swap.
    /// </summary>
    internal View? CurrentView { get; private set; }

    /// <summary>
    /// Replaces the current view with <paramref name="newView"/>, disposing the previous one.
    /// </summary>
    /// <param name="newView">The view to show, laid out to fill the container.</param>
    internal void Replace(View newView)
    {
        if (CurrentView is not null)
        {
            Container.Remove(CurrentView);
            CurrentView.Dispose();
        }

        newView.X = 0;
        newView.Y = 0;
        newView.Width = Dim.Fill();
        newView.Height = Dim.Fill();

        CurrentView = newView;
        Container.Add(CurrentView);
        Container.SetNeedsDraw();
    }

    /// <inheritdoc/>
    public void Dispose() => CurrentView?.Dispose();
}

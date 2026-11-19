using Refedle.App.Tui.UI.Views;
using Refedle.Engine.IO.DrillDown;
using Terminal.Gui.ViewBase;

namespace Refedle.App.Tui.UI;

/// <summary>
/// Owns the <see cref="BreadcrumbBar"/> and keeps it in sync with <see cref="AppState.CurrentKeyPath"/>.
/// </summary>
/// <param name="state">The application state that stores the current key path.</param>
/// <remarks>
/// Threading contract: the breadcrumb bar is UI-thread exclusive. <see cref="Update"/> and
/// <see cref="Clear"/> must be called on the UI thread.
/// </remarks>
internal sealed class BreadcrumbPresenter(AppState state)
{
    private readonly BreadcrumbBar _breadcrumbBar = new();

    /// <summary>
    /// Gets the breadcrumb view to add to the window.
    /// </summary>
    internal View View => _breadcrumbBar;

    /// <summary>
    /// Updates the breadcrumb bar to reflect the current location and stores it on <see cref="AppState"/>.
    /// </summary>
    /// <param name="path">The ordered path segments from root to the current location.</param>
    /// <param name="collapseIndices">
    /// When <c>true</c>, array indices render as <c>"[*]"</c> (Full Aggregation DrillDown).
    /// </param>
    internal void Update(IReadOnlyList<KeyPathSegment> path, bool collapseIndices)
    {
        state.SetCurrentKeyPath(path);
        _breadcrumbBar.SetPath(path, collapseIndices);
    }

    /// <summary>
    /// Blanks the breadcrumb bar and resets <see cref="AppState.CurrentKeyPath"/> for modes with no
    /// JSON hierarchy (<see cref="ViewMode.CsvTable"/>, <see cref="ViewMode.JsonLinesTable"/>,
    /// <see cref="ViewMode.FileSelection"/>). Unlike <see cref="Update"/> with an empty
    /// path, this does not render <c>"root"</c>.
    /// </summary>
    internal void Clear()
    {
        state.SetCurrentKeyPath([]);
        _breadcrumbBar.Clear();
    }
}

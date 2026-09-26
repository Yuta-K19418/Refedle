using Refedle.Engine.IO.DrillDown;
using Terminal.Gui.ViewBase;

namespace Refedle.App.Tui.Ui.Views;

/// <summary>
/// A single-row bar directly below the <c>MenuBar</c> that renders the current JSON hierarchy
/// location as a breadcrumb. Display-only; <see cref="SetPath"/> updates the displayed text.
/// </summary>
internal sealed class BreadcrumbBar : View
{
    internal BreadcrumbBar()
    {
        X = 0;
        Y = 1; // Directly below the MenuBar (row 0)
        Width = Dim.Fill();
        Height = 1;
    }

    /// <summary>
    /// Renders <paramref name="path"/> as the breadcrumb text, collapsing array indices to
    /// <c>"[*]"</c> when <paramref name="collapseIndices"/> is <c>true</c>.
    /// </summary>
    /// <param name="path">The ordered path segments from root to the current location.</param>
    /// <param name="collapseIndices">
    /// When <c>true</c>, array-element indices render as <c>"[*]"</c> (Full Aggregation DrillDown).
    /// </param>
    internal void SetPath(IReadOnlyList<KeyPathSegment> path, bool collapseIndices)
    {
        Text = KeyPathFormatter.Format(path, collapseIndices);
    }

    /// <summary>
    /// Blanks the breadcrumb text for views with no JSON hierarchy (e.g. <c>CsvTable</c>).
    /// Unlike <see cref="SetPath"/> with an empty path, this does not render <c>"root"</c>.
    /// </summary>
    internal void Clear()
    {
        Text = string.Empty;
    }
}

using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.TestHelpers.App.Tui.UI;

/// <summary>Lookup helpers for the filter-scan progress overlay.</summary>
internal static class ProgressOverlayViews
{
    // The overlay has no progress bar; its percentage label is the one view that is unique to it
    internal static bool IsProgressOverlayLabel(View view) =>
        view is Label label && label.Text.StartsWith(" Filtering", StringComparison.Ordinal);
}

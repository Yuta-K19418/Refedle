using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.IntegrationTests.App.Tui.UI.ViewManagerSupport;

/// <summary>View-tree helpers shared by the ViewManager test partials.</summary>
internal static class ViewManagerTestViews
{
    internal static IEnumerable<View> SelfAndDescendants(View view)
    {
        Stack<View> pending = new();
        pending.Push(view);

        while (pending.Count > 0)
        {
            View current = pending.Pop();
            yield return current;

            foreach (View child in current.SubViews)
            {
                pending.Push(child);
            }
        }
    }

    // The overlay has no progress bar; its percentage label is the one view that is unique to it
    internal static bool IsProgressOverlayLabel(View view) =>
        view is Label label && label.Text.StartsWith(" Filtering", StringComparison.Ordinal);

    internal static string? GetItemCountText(Window window) =>
        window.SubViews
            .OfType<Label>()
            .Select(static label => label.Text)
            .SingleOrDefault(static text => text.EndsWith(" items", StringComparison.Ordinal));
}

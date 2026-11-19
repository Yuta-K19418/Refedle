using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

/// <summary>Parent window and label lookup shared by the progress overlay tests.</summary>
internal static class OverlayTestViews
{
    internal static Window CreateParent() => new() { Width = Dim.Fill(), Height = Dim.Fill() };

    internal static Label GetLabel(View parent) => parent.SubViews.OfType<Label>().Single();
}

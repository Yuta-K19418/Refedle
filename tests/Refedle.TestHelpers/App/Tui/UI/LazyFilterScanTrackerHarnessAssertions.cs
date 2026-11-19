using AwesomeAssertions;
using Terminal.Gui.Views;

namespace Refedle.TestHelpers.App.Tui.UI;

/// <summary>Assertion helpers over <see cref="LazyFilterScanTrackerHarness"/>.</summary>
internal static class LazyFilterScanTrackerHarnessAssertions
{
    internal static Label GetOverlayLabel(LazyFilterScanTrackerHarness harness) =>
        harness.OverlayHost.SubViews.OfType<Label>().Should().ContainSingle().Subject;
}

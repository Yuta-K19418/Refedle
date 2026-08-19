using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task VimKeys_OnJsonArrayTree_MoveCursorAndViewport()
    {
        // Arrange
        var elements = Enumerable.Range(0, 30).Select(i => $$"""{"name":"Item{{i}}","age":{{i}}}""");
        var content = "[" + string.Join(",", elements) + "]";
        var inputFile = _testDirectory.CreateFile("input.json", content);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        var initialLines = await Harness.WaitForContentsAsync("[0]:");
        initialLines.Should().Contain(line => line.Contains("[0]:", StringComparison.Ordinal));

        // The initially selected node is "[0]:", rendered at screen row 3 (below the window
        // border, menu bar, and "root" line) and column 1 (the tree line's own prefix character,
        // e.g. "├" or "└").
        var initialCell = await Harness.GetSelectedCellAsync();
        initialCell.Should().Be((3, 1));

        // Act & Assert: j/k move the selection to the neighboring node.
        Harness.SendKey(KeyCode.J);
        var afterDown = await Harness.WaitForSelectedCellAsync(cell => cell == (4, 1));
        afterDown.Should().Be((4, 1));

        Harness.SendKey(KeyCode.K);
        var afterUp = await Harness.WaitForSelectedCellAsync(cell => cell == (3, 1));
        afterUp.Should().Be((3, 1));

        // h/l map to collapse/expand (Terminal.Gui's CursorLeft/CursorRight), which is a no-op
        // here since these array-element nodes render fully expanded already; the selection
        // itself must stay put rather than moving to a different node.
        Harness.SendKey(KeyCode.L);
        var afterRight = await Harness.GetSelectedCellAsync();
        afterRight.Should().Be((3, 1));

        Harness.SendKey(KeyCode.H);
        var afterLeft = await Harness.GetSelectedCellAsync();
        afterLeft.Should().Be((3, 1));

        // G jumps to the last node: the viewport scrolls so [0]: is no longer visible.
        Harness.SendKey(KeyCode.G | KeyCode.ShiftMask);
        var afterGoToEnd = await Harness.WaitForContentsAsync("[29]:");
        afterGoToEnd.Should().NotContain(line => line.Contains("[0]:", StringComparison.Ordinal));

        // gg jumps back to the first node.
        Harness.SendKey(KeyCode.G);
        Harness.SendKey(KeyCode.G);
        var afterGoToFirst = await Harness.WaitForContentsAsync("[0]:");
        afterGoToFirst.Should().NotContain(line => line.Contains("[29]:", StringComparison.Ordinal));

        // d pages down: the viewport scrolls so a later node becomes visible.
        Harness.SendKey(KeyCode.D);
        var afterPageDown = await Harness.WaitForContentsAsync("[19]:");
        afterPageDown.Should().NotContain(line => line.Contains("[0]:", StringComparison.Ordinal));

        // u pages back up to the first node.
        Harness.SendKey(KeyCode.U);
        var afterPageUp = await Harness.WaitForContentsAsync("[0]:");
        afterPageUp.Should().Contain(line => line.Contains("[0]:", StringComparison.Ordinal));
    }
}

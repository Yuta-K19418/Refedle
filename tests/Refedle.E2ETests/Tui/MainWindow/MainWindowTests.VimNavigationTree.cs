using AwesomeAssertions;
using Refedle.App.Tui.UI;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task VimKeys_OnJsonArrayTree_MoveCursorAndViewport()
    {
        // Arrange
        IEnumerable<string> elements = Enumerable.Range(0, 30).Select(i => $$"""{"name":"Item{{i}}","age":{{i}}}""");
        string content = "[" + string.Join(",", elements) + "]";
        string inputFile = _testDirectory.CreateFile("input.json", content);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        string[] initialLines = await Harness.WaitForContentsAsync("[0]:");
        initialLines.Should().Contain(line => line.Contains("[0]:", StringComparison.Ordinal));

        // The initially selected node is "[0]:", rendered at screen row 3 (below the window
        // border, menu bar, and "root" line) and column 1 (the tree line's own prefix character,
        // e.g. "├" or "└").
        (int Row, int Col)? initialCell = await Harness.WaitForSelectedCellAsync(cell => cell == (3, 1));
        initialCell.Should().Be((3, 1));

        // Act & Assert: j/k move the selection to the neighboring node.
        Harness.SendKey(KeyCode.J);
        (int Row, int Col)? afterDown = await Harness.WaitForSelectedCellAsync(cell => cell == (4, 1));
        afterDown.Should().Be((4, 1));

        Harness.SendKey(KeyCode.K);
        (int Row, int Col)? afterUp = await Harness.WaitForSelectedCellAsync(cell => cell == (3, 1));
        afterUp.Should().Be((3, 1));

        // In the tree, h/l do not collapse or expand (Enter does), so the selection must stay put.
        // A pre-key frame also shows (3, 1), so wait for the key to be processed and drawn before
        // reading; otherwise this passes even if the key moved the selection.
        Harness.SendKey(KeyCode.L);
        await Harness.WaitForInputProcessedAsync();
        (int Row, int Col)? afterRight = await Harness.GetSelectedCellAsync();
        afterRight.Should().Be((3, 1));

        Harness.SendKey(KeyCode.H);
        await Harness.WaitForInputProcessedAsync();
        (int Row, int Col)? afterLeft = await Harness.GetSelectedCellAsync();
        afterLeft.Should().Be((3, 1));

        // G jumps to the last node: the viewport scrolls so [0]: is no longer visible.
        Harness.SendKey(KeyCode.G | KeyCode.ShiftMask);
        string[] afterGoToEnd = await Harness.WaitForContentsAsync("[29]:");
        afterGoToEnd.Should().NotContain(line => line.Contains("[0]:", StringComparison.Ordinal));

        // gg jumps back to the first node.
        Harness.SendKey(KeyCode.G);
        Harness.SendKey(KeyCode.G);
        string[] afterGoToFirst = await Harness.WaitForContentsAsync("[0]:");
        afterGoToFirst.Should().NotContain(line => line.Contains("[29]:", StringComparison.Ordinal));

        // d pages down: the viewport scrolls so a later node becomes visible.
        Harness.SendKey(KeyCode.D);
        string[] afterPageDown = await Harness.WaitForContentsAsync("[19]:");
        afterPageDown.Should().NotContain(line => line.Contains("[0]:", StringComparison.Ordinal));

        // u pages back up to the first node.
        Harness.SendKey(KeyCode.U);
        string[] afterPageUp = await Harness.WaitForContentsAsync("[0]:");
        afterPageUp.Should().Contain(line => line.Contains("[0]:", StringComparison.Ordinal));
    }
}

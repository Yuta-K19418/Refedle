using AwesomeAssertions;
using Refedle.App.Tui.UI;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task VimKeys_OnCsvTable_MoveCursorAndViewport()
    {
        // Arrange
        IEnumerable<string> rows = Enumerable.Range(1, 25).Select(i => $"Row{i},{i}");
        string content = "name,age\n" + string.Join("\n", rows) + "\n";
        string inputFile = _testDirectory.CreateFile("input.csv", content);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        // A trailing space disambiguates "Row1" from "Row10".."Row19" in the rendered, padded
        // table cell (the source CSV's "," delimiter is not preserved in the rendered table).
        string[] initialLines = await Harness.WaitForContentsAsync("Row1 ");
        initialLines.Should().Contain(line => line.Contains("Row1 ", StringComparison.Ordinal));

        // The initially selected cell (Row1, name) renders at screen row 6, column 2. Each
        // column is 12 screen columns wide including its separator, so the "age" column's
        // selected cell starts at column 14.
        (int Row, int Col)? initialCell = await Harness.WaitForSelectedCellAsync(cell => cell == (6, 2));
        initialCell.Should().Be((6, 2));

        // Act & Assert: h/j/k/l each move the selection highlight by exactly one row/column.
        Harness.SendKey(KeyCode.L);
        (int Row, int Col)? afterRight = await Harness.WaitForSelectedCellAsync(cell => cell == (6, 14));
        afterRight.Should().Be((6, 14));

        Harness.SendKey(KeyCode.J);
        (int Row, int Col)? afterDown = await Harness.WaitForSelectedCellAsync(cell => cell == (7, 14));
        afterDown.Should().Be((7, 14));

        Harness.SendKey(KeyCode.K);
        (int Row, int Col)? afterUp = await Harness.WaitForSelectedCellAsync(cell => cell == (6, 14));
        afterUp.Should().Be((6, 14));

        Harness.SendKey(KeyCode.H);
        (int Row, int Col)? afterLeft = await Harness.WaitForSelectedCellAsync(cell => cell == (6, 2));
        afterLeft.Should().Be((6, 2));

        // G jumps to the last row: the viewport scrolls so Row1 is no longer visible.
        Harness.SendKey(KeyCode.G | KeyCode.ShiftMask);
        string[] afterGoToEnd = await Harness.WaitForContentsAsync("Row25 ");
        afterGoToEnd.Should().NotContain(line => line.Contains("Row1 ", StringComparison.Ordinal));

        // gg jumps back to the first row.
        Harness.SendKey(KeyCode.G);
        Harness.SendKey(KeyCode.G);
        string[] afterGoToFirst = await Harness.WaitForContentsAsync("Row1 ");
        afterGoToFirst.Should().NotContain(line => line.Contains("Row25 ", StringComparison.Ordinal));

        // d pages down: the viewport scrolls so a later row becomes visible.
        Harness.SendKey(KeyCode.D);
        string[] afterPageDown = await Harness.WaitForContentsAsync("Row17 ");
        afterPageDown.Should().NotContain(line => line.Contains("Row1 ", StringComparison.Ordinal));

        // u pages back up to the first row.
        Harness.SendKey(KeyCode.U);
        string[] afterPageUp = await Harness.WaitForContentsAsync("Row1 ");
        afterPageUp.Should().Contain(line => line.Contains("Row1 ", StringComparison.Ordinal));
    }
}

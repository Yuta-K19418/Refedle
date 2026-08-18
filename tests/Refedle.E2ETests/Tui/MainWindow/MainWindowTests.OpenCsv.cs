using AwesomeAssertions;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task OpenKey_WithCsvFileSelectedInOpenDialog_RendersHeaderAndDataRowsOnceIndexed()
    {
        // Arrange
        var csvContent = """
            name,age
            Alice,30
            Bob,25
            Charlie,35
            """;
        var inputFile = _testDirectory.CreateFile("input.csv", csvContent);

        // Act: drive the real 'o'-key flow. The path field is pre-filled with the dialog's
        // current directory, so clear it (same pattern as the Save tests) before typing
        // the absolute path; Enter then selects the listed file and closes the dialog.
        Harness.SendKey(KeyCode.O);
        await Harness.WaitForContentsAsync("Open File");
        Harness.SendKey(KeyCode.Home);
        Harness.SendKey(KeyCode.End | KeyCode.ShiftMask);
        Harness.SendKey(KeyCode.Delete);
        Harness.SendText(inputFile);
        await Harness.WaitForContentsAsync("input.csv");
        Harness.SendKey(KeyCode.Enter);
        var lines = await Harness.WaitForContentsAsync("Charlie", "35");

        // Assert
        // Rows 0-3 are the window border, "File" menu bar, and blank spacer above the table's
        // own top border; row 5 is the table's header separator. Rows 4/6/7/8 are the header
        // and data rows.
        lines[4].Should().Contain("name").And.Contain("age");
        lines[6].Should().Contain("Alice").And.Contain("30");
        lines[7].Should().Contain("Bob").And.Contain("25");
        lines[8].Should().Contain("Charlie").And.Contain("35");
    }
}

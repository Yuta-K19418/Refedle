using AwesomeAssertions;
using Refedle.App;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    private const string TestCsvContent = """
        name,age
        Alice,30
        Bob,25
        Charlie,35
        """;

    [Fact]
    public async Task ScheduleStartupLoad_WithCsvFile_RendersHeaderAndDataRowsOnceIndexed()
    {
        // Arrange
        var inputFile = _testDirectory.CreateFile("input.csv", TestCsvContent);

        // Act
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
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

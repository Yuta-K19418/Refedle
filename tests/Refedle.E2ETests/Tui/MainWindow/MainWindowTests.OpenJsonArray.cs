using AwesomeAssertions;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task OpenKey_WithJsonArrayFileSelectedInOpenDialog_RendersTreeNodes()
    {
        // Arrange
        var content = """
            [
              {"name":"Item1","age":1},
              {"name":"Item2","age":2},
              {"name":"Item3","age":3}
            ]
            """;
        var inputFile = _testDirectory.CreateFile("input.json", content);

        // Act: drive the real 'o'-key flow. The path field is pre-filled with the dialog's
        // current directory, so clear it (same pattern as the Save tests) before typing
        // the absolute path; Enter then selects the listed file and closes the dialog.
        Harness.SendKey(KeyCode.O);
        await Harness.WaitForContentsAsync("Open File");
        Harness.SendKey(KeyCode.Home);
        Harness.SendKey(KeyCode.End | KeyCode.ShiftMask);
        Harness.SendKey(KeyCode.Delete);
        Harness.SendText(inputFile);
        await Harness.WaitForContentsAsync("input.json");
        Harness.SendKey(KeyCode.Enter);
        var lines = await Harness.WaitForContentsAsync("[2]:", "Object: 2 properties");

        // Assert
        lines.Should().Contain(line => line.Contains("[0]:", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("[1]:", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("[2]:", StringComparison.Ordinal));
    }
}

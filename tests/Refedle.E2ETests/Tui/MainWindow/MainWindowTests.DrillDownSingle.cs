using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task DrillDown_OnJsonObjectTree_RendersSingleModeFocusedTable()
    {
        // Arrange
        var content = """{"items":[{"name":"A","val":1},{"name":"B","val":2}]}""";
        var inputFile = _testDirectory.CreateFile("input.json", content);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        await Harness.WaitForContentsAsync("items", "Array: 2 items");

        // Act: the sole "items" root node is already selected; x opens the DrillDown menu.
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.Enter);
        var lines = await Harness.WaitForContentsAsync("name (text)", "val (number)");

        // Assert
        lines.Should().Contain(line => line.Contains("[0]", StringComparison.Ordinal) && line.Contains('A') && line.Contains('1'));
        lines.Should().Contain(line => line.Contains("[1]", StringComparison.Ordinal) && line.Contains('B') && line.Contains('2'));
    }
}

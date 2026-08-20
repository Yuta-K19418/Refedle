using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task ActionMenu_CastColumnOnCsvTable_RendersNewColumnTypeSuffix()
    {
        // Arrange
        var csvContent = """
            name,age
            Alice,30
            Bob,25
            Charlie,35
            """;
        var inputFile = _testDirectory.CreateFile("input.csv", csvContent);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        await Harness.WaitForContentsAsync("Charlie", "35");

        // Act: move to the "age" column, open Cast (3rd item), pick FloatingPoint (3rd option).
        Harness.SendKey(KeyCode.L);
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.Enter);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.Enter);
        var lines = await Harness.WaitForContentsAsync("age (float)");

        // Assert
        lines.Should().NotContain(line => line.Contains("age (number)", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("age (float)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActionMenu_CastColumnOnFocusedTable_RendersNewColumnTypeSuffix()
    {
        // Arrange: DrillDown from a JSON Array tree into the FocusedTable view.
        var content = """
            [
              {"name":"Alice","age":30},
              {"name":"Bob","age":25},
              {"name":"Charlie","age":35}
            ]
            """;
        var inputFile = _testDirectory.CreateFile("input.json", content);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        await Harness.WaitForContentsAsync("[0]:");
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.Enter);
        await Harness.WaitForContentsAsync("name (text)", "age (number)");

        // Act: column 0 is the "#" pseudo column; move right twice to "age", open Cast
        // (3rd item), pick FloatingPoint (3rd option).
        Harness.SendKey(KeyCode.L);
        Harness.SendKey(KeyCode.L);
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.Enter);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.Enter);
        var lines = await Harness.WaitForContentsAsync("age (float)");

        // Assert
        lines.Should().NotContain(line => line.Contains("age (number)", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("age (float)", StringComparison.Ordinal));
    }
}

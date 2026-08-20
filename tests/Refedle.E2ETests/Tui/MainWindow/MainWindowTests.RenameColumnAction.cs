using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task ActionMenu_RenameColumnOnCsvTable_RendersNewHeaderWithUnchangedValues()
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

        // Act
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.Enter);
        // The text field is pre-filled with the current name "name" (4 chars); clear it
        // before typing the replacement.
        Harness.SendKey(KeyCode.Backspace);
        Harness.SendKey(KeyCode.Backspace);
        Harness.SendKey(KeyCode.Backspace);
        Harness.SendKey(KeyCode.Backspace);
        Harness.SendText("years");
        // Queued keys drain one per UI-loop iteration, so typing a multi-character value can take
        // longer than a short fixed delay would allow; wait for it to fully land before submitting.
        await Harness.WaitForContentsAsync("New name: years");
        Harness.SendKey(KeyCode.Enter);
        var lines = await Harness.WaitForContentsAsync("years (text)");

        // Assert
        lines.Should().Contain(line => line.Contains("years (text)", StringComparison.Ordinal));
        lines.Should().NotContain(line => line.Contains("name (text)", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("Alice", StringComparison.Ordinal) && line.Contains("30", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActionMenu_RenameColumnOnFocusedTable_RendersNewHeaderWithUnchangedValues()
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

        // Act: column 0 is the "#" pseudo column, so move right to "name" first.
        Harness.SendKey(KeyCode.L);
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.Enter);
        // The text field is pre-filled with the current name "name" (4 chars); clear it
        // before typing the replacement.
        Harness.SendKey(KeyCode.Backspace);
        Harness.SendKey(KeyCode.Backspace);
        Harness.SendKey(KeyCode.Backspace);
        Harness.SendKey(KeyCode.Backspace);
        Harness.SendText("years");
        // Queued keys drain one per UI-loop iteration, so typing a multi-character value can take
        // longer than a short fixed delay would allow; wait for it to fully land before submitting.
        await Harness.WaitForContentsAsync("New name: years");
        Harness.SendKey(KeyCode.Enter);
        var lines = await Harness.WaitForContentsAsync("years (text)");

        // Assert
        lines.Should().Contain(line => line.Contains("years (text)", StringComparison.Ordinal));
        lines.Should().NotContain(line => line.Contains("name (text)", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("Alice", StringComparison.Ordinal) && line.Contains("30", StringComparison.Ordinal));
    }
}

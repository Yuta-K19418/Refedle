using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task ActionMenu_FillColumnOnCsvTable_RendersFillValueInEveryCellOfColumn()
    {
        // Arrange
        var csvContent = """
            name,email
            Alice,alice@example.com
            Bob,
            """;
        var inputFile = _testDirectory.CreateFile("input.csv", csvContent);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        await Harness.WaitForContentsAsync("Alice", "alice@example.com");

        // Act: move to the "email" column, open Fill (5th item), submit a mask value.
        Harness.SendKey(KeyCode.L);
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.Enter);
        await Harness.WaitForContentsAsync("Fill Column");
        Harness.SendText("***");
        // Queued keys drain one per UI-loop iteration, so typing a multi-character value can take
        // longer than a short fixed delay would allow; wait for it to fully land before submitting.
        await Harness.WaitForContentsAsync("Value: ***");
        Harness.SendKey(KeyCode.Enter);
        // Poll for the fill's observable effect rather than a fixed delay. The dialog's own text
        // also hides "alice@example.com" while open, so its title's absence is checked too, so
        // the wait doesn't return before the confirming key actually lands.
        var lines = await Harness.WaitForConditionAsync(
            l => l.All(line => !line.Contains("alice@example.com", StringComparison.Ordinal) && !line.Contains("Fill Column", StringComparison.Ordinal)),
            "dialog closed and \"alice@example.com\" replaced by the fill value");

        // Assert
        lines.Should().Contain(line => line.Contains("Alice", StringComparison.Ordinal) && line.Contains("***", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("Bob", StringComparison.Ordinal) && line.Contains("***", StringComparison.Ordinal));
        lines.Should().NotContain(line => line.Contains("alice@example.com", StringComparison.Ordinal));
    }
}

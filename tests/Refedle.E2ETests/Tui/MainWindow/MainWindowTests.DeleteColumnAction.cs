using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task ActionMenu_DeleteColumnOnCsvTable_RemovesColumnFromRenderedTable()
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
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.Enter);
        await Harness.WaitForContentsAsync("Delete column");
        Harness.SendKey(KeyCode.Enter);
        // "name (text)" disappearing (rather than some new text appearing) is the observable
        // effect of the deletion, so poll for its absence instead of a fixed delay.
        var lines = await Harness.WaitForConditionAsync(
            l => l.All(line => !line.Contains("name (text)", StringComparison.Ordinal)),
            "\"name (text)\" column removed");

        // Assert
        lines.Should().NotContain(line => line.Contains("name (text)", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("age (number)", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("30", StringComparison.Ordinal));
    }
}

using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task HelpKey_OnCsvTable_RendersKeyBindingsOverlay()
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

        // Act: the dialog is taller than the 80x24 test screen, so its title and top section
        // scroll off-screen once centered — assert on body text that stays within the viewport.
        Harness.SendKey((KeyCode)'?');
        var lines = await Harness.WaitForContentsAsync("Navigation");

        // Assert
        lines.Should().Contain(line => line.Contains("Navigation", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("gg", StringComparison.Ordinal) && line.Contains("Jump to first row", StringComparison.Ordinal));

        // Close the overlay so the harness can stop cleanly on disposal. '?' itself is a global
        // shortcut that reopens a new overlay instead of closing this one, so Esc is used instead.
        Harness.SendKey(KeyCode.Esc);
        await Harness.WaitForContentsAsync("Charlie", "35");
    }

    [Fact]
    public async Task HelpKey_OnJsonArrayTree_RendersKeyBindingsOverlay()
    {
        // Arrange
        var content = """[{"name":"Item1","age":1}]""";
        var inputFile = _testDirectory.CreateFile("input.json", content);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        await Harness.WaitForContentsAsync("[0]:");

        // Act
        Harness.SendKey((KeyCode)'?');
        var lines = await Harness.WaitForContentsAsync("Navigation");

        // Assert
        lines.Should().Contain(line => line.Contains("Navigation", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("gg", StringComparison.Ordinal) && line.Contains("Jump to first row", StringComparison.Ordinal));

        // Close the overlay so the harness can stop cleanly on disposal. '?' itself is a global
        // shortcut that reopens a new overlay instead of closing this one, so Esc is used instead.
        Harness.SendKey(KeyCode.Esc);
        await Harness.WaitForContentsAsync("[0]:");
    }
}

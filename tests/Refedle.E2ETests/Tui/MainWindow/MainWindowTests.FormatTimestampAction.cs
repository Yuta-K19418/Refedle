using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task ActionMenu_FormatTimestampColumnOnCsvTable_RendersReformattedTimestamps()
    {
        // Arrange
        var csvContent = """
            name,joined
            Alice,2026-01-15T10:30:00
            Bob,2026-03-02T08:05:00
            """;
        var inputFile = _testDirectory.CreateFile("input.csv", csvContent);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        await Harness.WaitForContentsAsync("Alice", "2026-01-15T10:30:00");

        // Act: move to the "joined" column, open Format Timestamp (6th item), submit a target format.
        Harness.SendKey(KeyCode.L);
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.Enter);
        await Harness.WaitForContentsAsync("Format Timestamp");
        Harness.SendText("yyyy-MM-dd");
        // Queued keys drain one per UI-loop iteration, so typing a multi-character value can take
        // longer than a short fixed delay would allow; wait for it to fully land before submitting.
        await Harness.WaitForContentsAsync("Target format: yyyy-MM-dd");
        Harness.SendKey(KeyCode.Enter);
        // The original values already contain "2026-01-15" as a prefix, so poll for "T10:30:00"
        // disappearing instead. The dialog's own text also hides it while open, so its title's
        // absence is checked too, so the wait doesn't return before the confirming key lands.
        var lines = await Harness.WaitForConditionAsync(
            l => l.All(line => !line.Contains("T10:30:00", StringComparison.Ordinal) && !line.Contains("Format Timestamp", StringComparison.Ordinal)),
            "dialog closed and \"T10:30:00\" reformatted away");

        // Assert
        lines.Should().Contain(line => line.Contains("Alice", StringComparison.Ordinal) && line.Contains("2026-01-15", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("Bob", StringComparison.Ordinal) && line.Contains("2026-03-02", StringComparison.Ordinal));
        lines.Should().NotContain(line => line.Contains("T10:30:00", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActionMenu_FormatTimestampColumnOnFocusedTable_RendersReformattedTimestamps()
    {
        // Arrange: DrillDown from a JSON Array tree into the FocusedTable view.
        var content = """
            [
              {"name":"Alice","joined":"2026-01-15T10:30:00"},
              {"name":"Bob","joined":"2026-03-02T08:05:00"}
            ]
            """;
        var inputFile = _testDirectory.CreateFile("input.json", content);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        await Harness.WaitForContentsAsync("[0]:");
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.Enter);
        await Harness.WaitForContentsAsync("name (text)", "joined (datetime)");

        // Act: column 0 is the "#" pseudo column; move right twice to "joined", open Format
        // Timestamp (6th item), submit a target format.
        Harness.SendKey(KeyCode.L);
        Harness.SendKey(KeyCode.L);
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.Enter);
        await Harness.WaitForContentsAsync("Format Timestamp");
        Harness.SendText("yyyy-MM-dd");
        // Queued keys drain one per UI-loop iteration, so typing a multi-character value can take
        // longer than a short fixed delay would allow; wait for it to fully land before submitting.
        await Harness.WaitForContentsAsync("Target format: yyyy-MM-dd");
        Harness.SendKey(KeyCode.Enter);
        // The original values already contain "2026-01-15" as a prefix, so poll for "T10:30:00"
        // disappearing instead. The dialog's own text also hides it while open, so its title's
        // absence is checked too, so the wait doesn't return before the confirming key lands.
        var lines = await Harness.WaitForConditionAsync(
            l => l.All(line => !line.Contains("T10:30:00", StringComparison.Ordinal) && !line.Contains("Format Timestamp", StringComparison.Ordinal)),
            "dialog closed and \"T10:30:00\" reformatted away");

        // Assert
        lines.Should().Contain(line => line.Contains("Alice", StringComparison.Ordinal) && line.Contains("2026-01-15", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("Bob", StringComparison.Ordinal) && line.Contains("2026-03-02", StringComparison.Ordinal));
        lines.Should().NotContain(line => line.Contains("T10:30:00", StringComparison.Ordinal));
    }
}

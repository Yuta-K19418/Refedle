using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task ActionMenu_FilterColumnOnCsvTable_RendersOnlyMatchingRows()
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

        // Act: Filter is the 4th item; accept the default Equals/Text operator pair and filter
        // the "name" column for an exact match on "Alice".
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.Enter);
        Harness.SendKey(KeyCode.Enter);
        Harness.SendKey(KeyCode.Enter);
        Harness.SendText("Alice");
        // Queued keys drain one per UI-loop iteration, so typing a multi-character value can take
        // longer than a short fixed delay would allow; wait for it to fully land before submitting.
        await Harness.WaitForContentsAsync("Value: Alice");
        Harness.SendKey(KeyCode.Enter);
        // "Charlie" disappearing (rather than some new text appearing) is the observable effect
        // of the filter, so poll for its absence instead of a fixed delay.
        var lines = await Harness.WaitForConditionAsync(
            l => l.All(line => !line.Contains("Charlie", StringComparison.Ordinal)),
            "\"Charlie\" filtered out");

        // Assert
        lines.Should().Contain(line => line.Contains("Alice", StringComparison.Ordinal) && line.Contains("30", StringComparison.Ordinal));
        lines.Should().NotContain(line => line.Contains("Bob", StringComparison.Ordinal));
        lines.Should().NotContain(line => line.Contains("Charlie", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActionMenu_FilterColumnOnFocusedTable_RendersOnlyMatchingRows()
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

        // Act: column 0 is the "#" pseudo column, so move right to "name" first. Filter is
        // the 4th item; accept the default Equals/Text operator pair and filter the "name"
        // column for an exact match on "Alice".
        Harness.SendKey(KeyCode.L);
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.J);
        Harness.SendKey(KeyCode.Enter);
        Harness.SendKey(KeyCode.Enter);
        Harness.SendKey(KeyCode.Enter);
        Harness.SendText("Alice");
        // Queued keys drain one per UI-loop iteration, so typing a multi-character value can take
        // longer than a short fixed delay would allow; wait for it to fully land before submitting.
        await Harness.WaitForContentsAsync("Value: Alice");
        Harness.SendKey(KeyCode.Enter);
        // The dialog hides the table while open, so also require it to be absent, or the
        // predicate can succeed before the submitted Enter takes effect and hand a dialog frame
        // to the assertions below.
        var lines = await Harness.WaitForConditionAsync(
            l => l.All(line => !line.Contains("Charlie", StringComparison.Ordinal) && !line.Contains("Filter Column", StringComparison.Ordinal)),
            "dialog closed and \"Charlie\" filtered out");

        // Assert
        lines.Should().Contain(line => line.Contains("Alice", StringComparison.Ordinal) && line.Contains("30", StringComparison.Ordinal));
        lines.Should().NotContain(line => line.Contains("Bob", StringComparison.Ordinal));
        lines.Should().NotContain(line => line.Contains("Charlie", StringComparison.Ordinal));
    }
}

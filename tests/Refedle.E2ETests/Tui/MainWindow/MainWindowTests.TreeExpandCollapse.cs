using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task EnterKey_OnJsonArrayTree_ExpandsAndCollapsesSelectedNode()
    {
        // Arrange
        var content = """
            [
              {"name":"Item1","age":1},
              {"name":"Item2","age":2}
            ]
            """;
        var inputFile = _testDirectory.CreateFile("input.json", content);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        // Each element renders collapsed as a single "[N]: {Object: N properties}" line.
        await Harness.WaitForContentsAsync("[0]: {Object: 2 properties}");

        // Act: Enter expands the selected "[0]:" node; its property leaves render on
        // separate lines, so each is waited for individually.
        Harness.SendKey(KeyCode.Enter);
        await Harness.WaitForContentsAsync("name: \"Item1\"");
        var expanded = await Harness.WaitForContentsAsync("age: 1");

        // Assert
        expanded.Should().Contain(line => line.Contains("name: \"Item1\"", StringComparison.Ordinal));
        expanded.Should().Contain(line => line.Contains("age: 1", StringComparison.Ordinal));

        // Act: a second Enter collapses the node again. The leaves disappearing (rather than
        // some new text appearing) is the observable effect, so poll for their absence.
        Harness.SendKey(KeyCode.Enter);
        var collapsed = await Harness.WaitForConditionAsync(
            lines => lines.All(line =>
                !line.Contains("name: \"Item1\"", StringComparison.Ordinal)
                && !line.Contains("age: 1", StringComparison.Ordinal)),
            "expanded property leaves collapsed away");

        // Assert
        collapsed.Should().NotContain(line => line.Contains("name: \"Item1\"", StringComparison.Ordinal));
        collapsed.Should().NotContain(line => line.Contains("age: 1", StringComparison.Ordinal));
    }
}

using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task DrillDown_OnJsonArrayTree_RendersUnionSchemaAcrossAllElements()
    {
        // Arrange: each element contributes a different property, so a regression that reads
        // only the first element's schema (instead of the union) would still pass a
        // same-schema fixture — this heterogeneous one renders "email" only via a real union.
        var content = """
            [
              {"name":"Item1","age":1},
              {"name":"Item2","email":"item2@example.com"},
              {"age":3,"email":"item3@example.com"}
            ]
            """;
        var inputFile = _testDirectory.CreateFile("input.json", content);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        await Harness.WaitForContentsAsync("[0]:");

        // Act: x opens a menu with a single "DrillDown" item on the root selection.
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.Enter);
        var lines = await Harness.WaitForContentsAsync("name", "age", "email");

        // Assert: every column contributed by any element is rendered together in the header,
        // and each element's own values still appear in its row even where other columns are
        // missing for that element.
        lines.Should().Contain(line =>
            line.Contains("name", StringComparison.Ordinal)
            && line.Contains("age", StringComparison.Ordinal)
            && line.Contains("email", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("Item1", StringComparison.Ordinal) && line.Contains('1'));
        lines.Should().Contain(line => line.Contains("Item2", StringComparison.Ordinal) && line.Contains("item2@example.com", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains('3') && line.Contains("item3@example.com", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("3 items", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DrillDownBack_FromFocusedTable_ReturnsToOriginatingJsonArrayTree()
    {
        // Arrange
        var content = """
            [
              {"name":"Item1","age":1},
              {"name":"Item2","email":"item2@example.com"},
              {"age":3,"email":"item3@example.com"}
            ]
            """;
        var inputFile = _testDirectory.CreateFile("input.json", content);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        await Harness.WaitForContentsAsync("[0]:");
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.Enter);
        await Harness.WaitForContentsAsync("name", "age", "email");

        // Act
        Harness.SendKey(KeyCode.Backspace);
        var lines = await Harness.WaitForContentsAsync("[0]:");

        // Assert
        lines.Should().Contain(line => line.Contains("[0]:", StringComparison.Ordinal));
        lines.Should().NotContain(line => line.Contains("name", StringComparison.Ordinal) && line.Contains("age", StringComparison.Ordinal));
    }
}

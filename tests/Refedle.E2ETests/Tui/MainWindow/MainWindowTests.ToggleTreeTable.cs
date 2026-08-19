using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task ToggleViewKey_OnJsonLinesTree_TogglesBetweenTreeAndTableViews()
    {
        // Arrange
        var content = """
            {"name":"Alice","age":30}
            {"name":"Bob","age":25}
            """;
        var inputFile = _testDirectory.CreateFile("input.jsonl", content);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        var treeLines = await Harness.WaitForContentsAsync("Line 1");
        treeLines.Should().Contain(line => line.Contains("Line 1", StringComparison.Ordinal));

        // Act: 't' switches the JSON Lines view to table mode.
        Harness.SendKey(KeyCode.T);
        var tableLines = await Harness.WaitForContentsAsync("name (text)", "age (number)");

        // Assert: the tree rendering is replaced by the table rendering.
        tableLines.Should().Contain(line => line.Contains("name (text)", StringComparison.Ordinal));
        tableLines.Should().Contain(line => line.Contains("Alice", StringComparison.Ordinal) && line.Contains("30", StringComparison.Ordinal));
        tableLines.Should().NotContain(line => line.Contains("Line 1", StringComparison.Ordinal));

        // Act: a second 't' switches back to tree mode. ToggleJsonLinesModeAsync's background
        // completion can otherwise swallow a key queued too soon after the toggle; a selected
        // cell being rendered is the observable signal that the table view is fully up.
        await Harness.WaitForSelectedCellAsync(cell => cell is not null);
        Harness.SendKey(KeyCode.T);
        var roundTripLines = await Harness.WaitForContentsAsync("Line 1");

        // Assert: the table rendering is replaced by the original tree rendering.
        roundTripLines.Should().Contain(line => line.Contains("Line 1", StringComparison.Ordinal));
        roundTripLines.Should().NotContain(line => line.Contains("name (text)", StringComparison.Ordinal));
        roundTripLines.Should().NotContain(line => line.Contains("age (number)", StringComparison.Ordinal));
    }
}

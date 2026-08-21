using AwesomeAssertions;
using Refedle.App;
using Refedle.E2ETests.Helpers;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task SaveAndLoadRecipe_SingleDrillDownFromJsonObject_RoundTripsKeyPathAndActionAcrossFreshSession()
    {
        // Arrange
        var content = """{"items":[{"name":"A","val":1},{"name":"B","val":2}]}""";
        var inputFile = _testDirectory.CreateFile("input.json", content);
        var savePath = Path.Combine(Environment.CurrentDirectory, "recipe-roundtrip-single-drilldown-test.yaml");
        File.Delete(savePath);
        try
        {
            Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
            await Harness.WaitForContentsAsync("items", "Array: 2 items");

            // Act: drill into "items", then rename "name" -> "label" inside the FocusedTable.
            Harness.SendKey(KeyCode.X);
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("name (text)", "val (number)");

            Harness.SendKey(KeyCode.L);
            Harness.SendKey(KeyCode.X);
            Harness.SendKey(KeyCode.Enter);
            // The text field is pre-filled with the current name "name" (4 chars); clear it
            // before typing the replacement.
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendText("label");
            await Harness.WaitForContentsAsync("New name: label");
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("label (text)");

            Harness.SendKey(KeyCode.S);
            await Harness.WaitForContentsAsync("Save Recipe");
            Harness.SendKey(KeyCode.Home);
            Harness.SendKey(KeyCode.End | KeyCode.ShiftMask);
            Harness.SendKey(KeyCode.Delete);
            Harness.SendText("recipe-roundtrip-single-drilldown-test.yaml");
            await Harness.WaitForContentsAsync("recipe-roundtrip-single-drilldown-test.yaml");
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("Recipe saved successfully");
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("label (text)");

            // Assert: the saved recipe recorded both the DrillDown location and the action.
            File.Exists(savePath).Should().BeTrue();
            var recipeContent = await File.ReadAllTextAsync(savePath);
            recipeContent.Should().Contain("drillDownKeyPath:");
            recipeContent.Should().Contain("key: \"items\"");
            recipeContent.Should().Contain("type: Rename");
            recipeContent.Should().Contain("oldName: \"name\"");
            recipeContent.Should().Contain("newName: \"label\"");

            // Act: load the same recipe alongside the same input file in a brand-new session.
            await Harness.DisposeAsync();
            _harness = await TuiTestHarness.StartAsync();
            Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile, savePath));

            // Assert: the fresh session lands directly on the DrillDown table with the recipe's
            // action already applied, without any further key input.
            var lines = await Harness.WaitForContentsAsync("label (text)");
            lines.Should().Contain(line => line.Contains("[0]", StringComparison.Ordinal) && line.Contains('A') && line.Contains('1'));
            lines.Should().Contain(line => line.Contains("[1]", StringComparison.Ordinal) && line.Contains('B') && line.Contains('2'));
        }
        finally
        {
            File.Delete(savePath);
        }
    }

    [Fact]
    public async Task SaveAndLoadRecipe_FullAggregationDrillDownFromJsonArray_RoundTripsKeyPathAndActionAcrossFreshSession()
    {
        // Arrange
        var content = """
            [
              {"name":"Alice","age":30},
              {"name":"Bob","age":25}
            ]
            """;
        var inputFile = _testDirectory.CreateFile("input.json", content);
        var savePath = Path.Combine(Environment.CurrentDirectory, "recipe-roundtrip-full-aggregation-test.yaml");
        File.Delete(savePath);
        try
        {
            Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
            await Harness.WaitForContentsAsync("[0]:");

            // Act: the root selection triggers Full Aggregation DrillDown (JSON Array format, no
            // key segment), then rename "name" -> "label" inside the FocusedTable.
            Harness.SendKey(KeyCode.X);
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("name (text)", "age (number)");

            Harness.SendKey(KeyCode.L);
            Harness.SendKey(KeyCode.X);
            Harness.SendKey(KeyCode.Enter);
            // The text field is pre-filled with the current name "name" (4 chars); clear it
            // before typing the replacement.
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendText("label");
            await Harness.WaitForContentsAsync("New name: label");
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("label (text)");

            Harness.SendKey(KeyCode.S);
            await Harness.WaitForContentsAsync("Save Recipe");
            Harness.SendKey(KeyCode.Home);
            Harness.SendKey(KeyCode.End | KeyCode.ShiftMask);
            Harness.SendKey(KeyCode.Delete);
            Harness.SendText("recipe-roundtrip-full-aggregation-test.yaml");
            await Harness.WaitForContentsAsync("recipe-roundtrip-full-aggregation-test.yaml");
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("Recipe saved successfully");
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("label (text)");

            // Assert: the root-level DrillDown has an empty KeyPath, and the action was recorded.
            File.Exists(savePath).Should().BeTrue();
            var recipeContent = await File.ReadAllTextAsync(savePath);
            recipeContent.Should().Contain("drillDownKeyPath: []");
            recipeContent.Should().Contain("type: Rename");
            recipeContent.Should().Contain("oldName: \"name\"");
            recipeContent.Should().Contain("newName: \"label\"");

            // Act: load the same recipe alongside the same input file in a brand-new session.
            await Harness.DisposeAsync();
            _harness = await TuiTestHarness.StartAsync();
            Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile, savePath));

            // Assert: the fresh session lands directly on the DrillDown table with the recipe's
            // action already applied, without any further key input.
            var lines = await Harness.WaitForContentsAsync("label (text)");
            lines.Should().Contain(line => line.Contains("Alice", StringComparison.Ordinal) && line.Contains("30", StringComparison.Ordinal));
            lines.Should().Contain(line => line.Contains("Bob", StringComparison.Ordinal) && line.Contains("25", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(savePath);
        }
    }
}

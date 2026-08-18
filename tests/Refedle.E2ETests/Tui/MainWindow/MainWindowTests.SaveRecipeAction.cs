using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task SaveRecipe_FromCsvTable_WritesPerformedActionToRecipeFile()
    {
        // Arrange
        var csvContent = """
            name,age
            Alice,30
            Bob,25
            Charlie,35
            """;
        var inputFile = _testDirectory.CreateFile("input.csv", csvContent);
        var savePath = Path.Combine(Environment.CurrentDirectory, "save-recipe-csv-test.yaml");
        File.Delete(savePath);
        try
        {
            Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
            await Harness.WaitForContentsAsync("Charlie", "35");

            // Act: rename "name" -> "years" so the action stack is non-empty, then save.
            Harness.SendKey(KeyCode.X);
            Harness.SendKey(KeyCode.Enter);
            // The text field is pre-filled with the current name "name" (4 chars); clear it
            // before typing the replacement.
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendText("years");
            // Queued keys drain one per UI-loop iteration, so typing a multi-character value can
            // take longer than a short fixed delay would allow; wait for it to fully land first.
            await Harness.WaitForContentsAsync("New name: years");
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("years (text)");

            Harness.SendKey(KeyCode.S);
            await Harness.WaitForContentsAsync("Save Recipe");
            Harness.SendKey(KeyCode.Home);
            Harness.SendKey(KeyCode.End | KeyCode.ShiftMask);
            Harness.SendKey(KeyCode.Delete);
            Harness.SendText("save-recipe-csv-test.yaml");
            await Harness.WaitForContentsAsync("save-recipe-csv-test.yaml");
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("Recipe saved successfully");
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("years (text)");

            // Assert
            File.Exists(savePath).Should().BeTrue();
            var recipeContent = await File.ReadAllTextAsync(savePath);
            recipeContent.Should().Contain("type: rename");
            recipeContent.Should().Contain("oldName: \"name\"");
            recipeContent.Should().Contain("newName: \"years\"");
        }
        finally
        {
            File.Delete(savePath);
        }
    }

    [Fact]
    public async Task SaveRecipe_FromJsonLinesTree_WritesPerformedActionToRecipeFile()
    {
        // Arrange
        var content = """
            {"name":"Alice","age":30}
            {"name":"Bob","age":25}
            """;
        var inputFile = _testDirectory.CreateFile("input.jsonl", content);
        var savePath = Path.Combine(Environment.CurrentDirectory, "save-recipe-jsonlinestree-test.yaml");
        File.Delete(savePath);
        try
        {
            Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
            await Harness.WaitForContentsAsync("Line 1");

            // Act: toggle to the table view to rename "age" -> "years", toggle back to the tree
            // view, then save while CurrentMode is JsonLinesTree.
            Harness.SendKey(KeyCode.T);
            await Harness.WaitForContentsAsync("age (number)");
            // ToggleJsonLinesModeAsync's background completion can otherwise swallow a key queued
            // too soon after the toggle; a selected cell being rendered is the observable signal
            // that the new table view is fully up and accepting input.
            await Harness.WaitForSelectedCellAsync(cell => cell is not null);
            Harness.SendKey(KeyCode.L);
            Harness.SendKey(KeyCode.X);
            await Harness.WaitForContentsAsync("Actions");
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("Rename Column");
            // The text field is pre-filled with the current name "age" (3 chars); clear it
            // before typing the replacement.
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendKey(KeyCode.Backspace);
            Harness.SendText("years");
            // Queued keys drain one per UI-loop iteration, so typing a multi-character value can
            // take longer than a short fixed delay would allow; wait for it to fully land first.
            await Harness.WaitForContentsAsync("New name: years");
            Harness.SendKey(KeyCode.Enter);
            // "age" is a number column, so the renamed header keeps the "(number)" type suffix.
            await Harness.WaitForContentsAsync("years (number)");
            Harness.SendKey(KeyCode.T);
            await Harness.WaitForContentsAsync("Line 1");

            Harness.SendKey(KeyCode.S);
            await Harness.WaitForContentsAsync("Save Recipe");
            Harness.SendKey(KeyCode.Home);
            Harness.SendKey(KeyCode.End | KeyCode.ShiftMask);
            Harness.SendKey(KeyCode.Delete);
            Harness.SendText("save-recipe-jsonlinestree-test.yaml");
            await Harness.WaitForContentsAsync("save-recipe-jsonlinestree-test.yaml");
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("Recipe saved successfully");
            Harness.SendKey(KeyCode.Enter);
            await Harness.WaitForContentsAsync("Line 1");

            // Assert
            File.Exists(savePath).Should().BeTrue();
            var recipeContent = await File.ReadAllTextAsync(savePath);
            recipeContent.Should().Contain("type: rename");
            recipeContent.Should().Contain("oldName: \"age\"");
            recipeContent.Should().Contain("newName: \"years\"");
        }
        finally
        {
            File.Delete(savePath);
        }
    }
}

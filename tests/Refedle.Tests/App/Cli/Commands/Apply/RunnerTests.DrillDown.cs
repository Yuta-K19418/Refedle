using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Cli.Commands.Apply;
using Refedle.App.Cli.Parsing;

namespace Refedle.Tests.App.Cli.Commands.Apply;

public sealed partial class RunnerTests
{
    [Fact]
    public async Task RunAsync_JsonObjectToCsv_WithDrillDownRecipe_WritesTheDrilledDownRows()
    {
        // Arrange
        string inputFile = CreateTestFile("input.json", """{"orders":[{"id":1,"item":"a"},{"id":2,"item":"b"}]}""");
        string recipeFile = CreateTestFile("recipe.yaml", "name: DrillDown\nactions: []\ndrillDownKeyPath:\n  - key: orders");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("id,item\n1,a\n2,b\n");
    }

    [Fact]
    public async Task RunAsync_JsonObjectToJsonLines_WithDrillDownFilterRecipe_AppliesTheActionToTheDrilledDownRows()
    {
        // Arrange — the Filter targets a column of the drilled-down table, not the base object.
        string inputFile = CreateTestFile("input.json", """{"orders":[{"id":1,"item":"a"},{"id":2,"item":"b"},{"id":3,"item":"c"}]}""");
        string recipeFile = CreateTestFile(
            "recipe.yaml",
            "name: DrillDown Filter\nactions:\n  - type: Filter\n    columnName: id\n    operator: GreaterThan\n    comparisonType: Number\n    value: 1\ndrillDownKeyPath:\n  - key: orders");
        string outputFile = Path.Combine(_testDir, "output.jsonl");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("{\"id\":2,\"item\":\"b\"}\n{\"id\":3,\"item\":\"c\"}\n");
    }

    [Fact]
    public async Task RunAsync_JsonObjectToJson_WithDrillDownRecipe_WritesTheDrilledDownRowsAsAJsonArray()
    {
        // Arrange
        string inputFile = CreateTestFile("input.json", """{"orders":[{"id":1,"item":"a"},{"id":2,"item":"b"}]}""");
        string recipeFile = CreateTestFile("recipe.yaml", "name: DrillDown\nactions: []\ndrillDownKeyPath:\n  - key: orders");
        string outputFile = Path.Combine(_testDir, "output.json");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("""[{"id":1,"item":"a"},{"id":2,"item":"b"}]""");
    }

    [Fact]
    public async Task RunAsync_JsonObjectToCsv_WithUtf8BomPrefixedInput_WritesTheDrilledDownRows()
    {
        // Arrange — a UTF-8 BOM at the start of the input must not break KeyPath resolution.
        string inputFile = CreateTestFileWithUtf8Bom(
            "input.json", """{"orders":[{"id":1,"item":"a"},{"id":2,"item":"b"}]}""");
        string recipeFile = CreateTestFile("recipe.yaml", "name: DrillDown\nactions: []\ndrillDownKeyPath:\n  - key: orders");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("id,item\n1,a\n2,b\n");
    }

    [Fact]
    public async Task RunAsync_JsonArrayToJson_WithUtf8BomPrefixedInput_WritesTheFullAggregationRows()
    {
        // Arrange — a UTF-8 BOM at the start of the input must not break element indexing.
        string inputFile = CreateTestFileWithUtf8Bom(
            "input.json",
            """[{"orders":[{"id":1,"item":"a"}]},{"orders":[{"id":2,"item":"b"},{"id":3,"item":"c"}]}]""");
        string recipeFile = CreateTestFile("recipe.yaml", "name: DrillDown\nactions: []\ndrillDownKeyPath:\n  - key: orders");
        string outputFile = Path.Combine(_testDir, "output.json");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("""[{"id":1,"item":"a"},{"id":2,"item":"b"},{"id":3,"item":"c"}]""");
    }

    [Fact]
    public async Task RunAsync_JsonArrayToJson_WithDrillDownRecipe_WritesTheFullAggregationRowsAsAJsonArray()
    {
        // Arrange — Full Aggregation: the KeyPath is traversed for every top-level array element.
        string inputFile = CreateTestFile(
            "input.json",
            """[{"orders":[{"id":1,"item":"a"}]},{"orders":[{"id":2,"item":"b"},{"id":3,"item":"c"}]}]""");
        string recipeFile = CreateTestFile("recipe.yaml", "name: DrillDown\nactions: []\ndrillDownKeyPath:\n  - key: orders");
        string outputFile = Path.Combine(_testDir, "output.json");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("""[{"id":1,"item":"a"},{"id":2,"item":"b"},{"id":3,"item":"c"}]""");
    }

    [Fact]
    public async Task RunAsync_JsonArrayToJson_WithDrillDownFilterRecipe_AppliesTheActionToAggregatedRows()
    {
        // Arrange — exercises the full JSON Array aggregation path with an action:
        // FullAggregationSchemaScanner -> ColumnNameResolver -> ActionApplier -> JsonArray reader filter.
        string inputFile = CreateTestFile(
            "input.json",
            """[{"orders":[{"id":1}]},{"orders":[{"id":2},{"id":3}]}]""");
        string recipeFile = CreateTestFile(
            "recipe.yaml",
            "name: DrillDown Filter\nactions:\n  - type: Filter\n    columnName: id\n    operator: GreaterThan\n    comparisonType: Number\n    value: 1\ndrillDownKeyPath:\n  - key: orders");
        string outputFile = Path.Combine(_testDir, "output.json");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("""[{"id":2},{"id":3}]""");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToJson_WithDrillDownFilterRecipe_AppliesTheActionToAggregatedRows()
    {
        // Arrange — before Phase 6 the Runner ignored recipe.DrillDownKeyPath for JSON Lines and
        // applied the recipe against the KeyPath-less base schema (the silent bug this fixes).
        string inputFile = CreateTestFile(
            "input.jsonl",
            "{\"orders\":[{\"id\":1}]}\n{\"orders\":[{\"id\":2},{\"id\":3}]}\n");
        string recipeFile = CreateTestFile(
            "recipe.yaml",
            "name: DrillDown Filter\nactions:\n  - type: Filter\n    columnName: id\n    operator: GreaterThan\n    comparisonType: Number\n    value: 1\ndrillDownKeyPath:\n  - key: orders");
        string outputFile = Path.Combine(_testDir, "output.json");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("""[{"id":2},{"id":3}]""");
    }

    [Fact]
    public async Task RunAsync_JsonObjectInput_WithBaseTableRecipe_ReturnsExitCode1()
    {
        // Arrange — a recipe with no DrillDown scope cannot be replayed against JSON Object input.
        string inputFile = CreateTestFile("input.json", """{"orders":[{"id":1}]}""");
        string recipeFile = CreateTestFile("recipe.yaml", "name: Base\nactions: []");
        string outputFile = Path.Combine(_testDir, "output.csv");
        Arguments args = new() { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        TestAppLogger logger = new();

        // Act
        ExitCode exitCode = await Runner.RunAsync(args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), new TestStatusReporter());

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().StartWith("Error validating recipe:");
        File.Exists(outputFile).Should().BeFalse();
    }
}

using AwesomeAssertions;
using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

public sealed partial class RunnerTests
{
    [Fact]
    public async Task RunAsync_JsonObjectToCsv_WithDrillDownRecipe_WritesTheDrilledDownRows()
    {
        // Arrange
        var inputFile = CreateTestFile("input.json", """{"orders":[{"id":1,"item":"a"},{"id":2,"item":"b"}]}""");
        var recipeFile = CreateTestFile("recipe.yaml", "name: DrillDown\nactions: []\ndrillDownKeyPath:\n  - key: orders");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("id,item\n1,a\n2,b\n");
    }

    [Fact]
    public async Task RunAsync_JsonObjectToJsonLines_WithDrillDownFilterRecipe_AppliesTheActionToTheDrilledDownRows()
    {
        // Arrange — the Filter targets a column of the drilled-down table, not the base object.
        var inputFile = CreateTestFile("input.json", """{"orders":[{"id":1,"item":"a"},{"id":2,"item":"b"},{"id":3,"item":"c"}]}""");
        var recipeFile = CreateTestFile(
            "recipe.yaml",
            "name: DrillDown Filter\nactions:\n  - type: Filter\n    columnName: id\n    operator: GreaterThan\n    comparisonType: Number\n    value: 1\ndrillDownKeyPath:\n  - key: orders");
        var outputFile = Path.Combine(_testDir, "output.jsonl");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("{\"id\":2,\"item\":\"b\"}\n{\"id\":3,\"item\":\"c\"}\n");
    }

    [Fact]
    public async Task RunAsync_JsonObjectToJson_WithDrillDownRecipe_WritesTheDrilledDownRowsAsAJsonArray()
    {
        // Arrange
        var inputFile = CreateTestFile("input.json", """{"orders":[{"id":1,"item":"a"},{"id":2,"item":"b"}]}""");
        var recipeFile = CreateTestFile("recipe.yaml", "name: DrillDown\nactions: []\ndrillDownKeyPath:\n  - key: orders");
        var outputFile = Path.Combine(_testDir, "output.json");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("""[{"id":1,"item":"a"},{"id":2,"item":"b"}]""");
    }

    [Fact]
    public async Task RunAsync_JsonArrayToJson_WithDrillDownRecipe_WritesTheFullAggregationRowsAsAJsonArray()
    {
        // Arrange — Full Aggregation: the KeyPath is traversed for every top-level array element.
        var inputFile = CreateTestFile(
            "input.json",
            """[{"orders":[{"id":1,"item":"a"}]},{"orders":[{"id":2,"item":"b"},{"id":3,"item":"c"}]}]""");
        var recipeFile = CreateTestFile("recipe.yaml", "name: DrillDown\nactions: []\ndrillDownKeyPath:\n  - key: orders");
        var outputFile = Path.Combine(_testDir, "output.json");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("""[{"id":1,"item":"a"},{"id":2,"item":"b"},{"id":3,"item":"c"}]""");
    }

    [Fact]
    public async Task RunAsync_JsonArrayToJson_WithDrillDownFilterRecipe_AppliesTheActionToAggregatedRows()
    {
        // Arrange — exercises the full JSON Array aggregation path with an action:
        // FullAggregationSchemaScanner -> ColumnNameResolver -> ActionApplier -> JsonArray reader filter.
        var inputFile = CreateTestFile(
            "input.json",
            """[{"orders":[{"id":1}]},{"orders":[{"id":2},{"id":3}]}]""");
        var recipeFile = CreateTestFile(
            "recipe.yaml",
            "name: DrillDown Filter\nactions:\n  - type: Filter\n    columnName: id\n    operator: GreaterThan\n    comparisonType: Number\n    value: 1\ndrillDownKeyPath:\n  - key: orders");
        var outputFile = Path.Combine(_testDir, "output.json");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Errors.Count.Should().Be(0);
        var output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("""[{"id":2},{"id":3}]""");
    }

    [Fact]
    public async Task RunAsync_JsonObjectInput_WithBaseTableRecipe_ReturnsExitCode1()
    {
        // Arrange — a recipe with no DrillDown scope cannot be replayed against JSON Object input.
        var inputFile = CreateTestFile("input.json", """{"orders":[{"id":1}]}""");
        var recipeFile = CreateTestFile("recipe.yaml", "name: Base\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        var args = new Arguments { InputFile = inputFile, RecipeFile = recipeFile, OutputFile = outputFile };
        var logger = new TestAppLogger();

        // Act
        var exitCode = await Runner.RunAsync(args, logger);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        logger.Errors.Should().ContainSingle().Which.Should().StartWith("Error validating recipe:");
        File.Exists(outputFile).Should().BeFalse();
    }
}

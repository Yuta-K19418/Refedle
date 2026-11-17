using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.Json;

public sealed partial class JsonOutputTests
{
    [Fact]
    public async Task Run_CsvToJson_WithFilterAction_ExitsWithZeroAndWritesOnlyMatchingRowsAsAJsonArray()
    {
        // Arrange
        string csvContent = """
            name,age
            Alice,30
            Bob,25
            Charlie,35
            """;
        string recipeYaml = """
            name: Filter age
            actions:
              - type: Filter
                columnName: age
                operator: GreaterThan
                comparisonType: Number
                value: 30
            """;
        string inputFile = _testDirectory.CreateFile("input.csv", csvContent);
        string recipeFile = _testDirectory.CreateFile("recipe.yaml", recipeYaml);
        string outputFile = Path.Combine(_testDirectory.Path, "output.json");

        // Act
        CliProcessResult result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, _testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        // "35" is numeric CSV text, so it is written as a JSON number, not a quoted string.
        output.Should().Be("""[{"name":"Charlie","age":35}]"""); // Alice (30) and Bob (25) filtered out (age <= 30)
    }

    [Fact]
    public async Task Run_JsonLinesToJson_WithFilterAction_ExitsWithZeroAndWritesOnlyMatchingRecordsAsAJsonArray()
    {
        // Arrange
        string jsonLinesContent = """
            {"name":"Alice","age":30}
            {"name":"Bob","age":25}
            {"name":"Charlie","age":35}
            """;
        string recipeYaml = """
            name: Filter age
            actions:
              - type: Filter
                columnName: age
                operator: GreaterThan
                comparisonType: Number
                value: 30
            """;
        string inputFile = _testDirectory.CreateFile("input.jsonl", jsonLinesContent);
        string recipeFile = _testDirectory.CreateFile("recipe.yaml", recipeYaml);
        string outputFile = Path.Combine(_testDirectory.Path, "output.json");

        // Act
        CliProcessResult result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, _testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("""[{"name":"Charlie","age":35}]"""); // Alice (30) and Bob (25) filtered out (age <= 30)
    }

    [Fact]
    public async Task Run_JsonLinesDrillDownToJson_WithFilterAction_ExitsWithZeroAndWritesTheFilteredDrilledDownRowsAsAJsonArray()
    {
        // Arrange — Full Aggregation over JSON Lines: "orders" is traversed for every line and the
        // Filter targets a drilled-down column.
        string jsonLinesContent = """
            {"orders":[{"id":1,"item":"a"}]}
            {"orders":[{"id":2,"item":"b"},{"id":3,"item":"c"}]}
            """;
        string recipeYaml = """
            name: Filter id
            actions:
              - type: Filter
                columnName: id
                operator: GreaterThan
                comparisonType: Number
                value: 1
            drillDownKeyPath:
              - key: orders
            """;
        string inputFile = _testDirectory.CreateFile("input.jsonl", jsonLinesContent);
        string recipeFile = _testDirectory.CreateFile("recipe.yaml", recipeYaml);
        string outputFile = Path.Combine(_testDirectory.Path, "output.json");

        // Act
        CliProcessResult result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, _testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("""[{"id":2,"item":"b"},{"id":3,"item":"c"}]"""); // id 1 filtered out (id <= 1)
    }

    [Fact]
    public async Task Run_JsonArrayDrillDownToJson_WithFilterAction_ExitsWithZeroAndWritesTheFilteredDrilledDownRowsAsAJsonArray()
    {
        // Arrange — Full Aggregation: "orders" is traversed for every top-level array element.
        string jsonArrayContent = """
            [{"orders":[{"id":1,"item":"a"}]},{"orders":[{"id":2,"item":"b"},{"id":3,"item":"c"}]}]
            """;
        string recipeYaml = """
            name: Filter id
            actions:
              - type: Filter
                columnName: id
                operator: GreaterThan
                comparisonType: Number
                value: 1
            drillDownKeyPath:
              - key: orders
            """;
        string inputFile = _testDirectory.CreateFile("input.json", jsonArrayContent);
        string recipeFile = _testDirectory.CreateFile("recipe.yaml", recipeYaml);
        string outputFile = Path.Combine(_testDirectory.Path, "output.json");

        // Act
        CliProcessResult result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, _testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("""[{"id":2,"item":"b"},{"id":3,"item":"c"}]"""); // id 1 filtered out (id <= 1)
    }

    [Fact]
    public async Task Run_JsonObjectDrillDownToJson_WithFilterAction_ExitsWithZeroAndWritesASingleRowStillWrappedInAnArray()
    {
        // Arrange — Single DrillDown; the Filter keeps exactly one row. ADR-1: the output stays a
        // JSON Array even for one row (`[{…}]`), never a bare object.
        string jsonObjectContent = """
            {"orders":[{"id":1,"item":"a"},{"id":2,"item":"b"},{"id":3,"item":"c"}]}
            """;
        string recipeYaml = """
            name: Filter id
            actions:
              - type: Filter
                columnName: id
                operator: GreaterThan
                comparisonType: Number
                value: 2
            drillDownKeyPath:
              - key: orders
            """;
        string inputFile = _testDirectory.CreateFile("input.json", jsonObjectContent);
        string recipeFile = _testDirectory.CreateFile("recipe.yaml", recipeYaml);
        string outputFile = Path.Combine(_testDirectory.Path, "output.json");

        // Act
        CliProcessResult result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, _testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        string output = await File.ReadAllTextAsync(outputFile);
        output.Should().Be("""[{"id":3,"item":"c"}]"""); // id 1 and id 2 filtered out (id <= 2)
    }
}

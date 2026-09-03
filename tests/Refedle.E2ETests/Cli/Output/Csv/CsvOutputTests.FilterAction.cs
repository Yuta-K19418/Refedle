using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.Csv;

public sealed partial class CsvOutputTests
{
    [Fact]
    public async Task Run_CsvToCsv_WithFilterAction_ExitsWithZeroAndWritesOnlyMatchingRows()
    {
        // Arrange
        var csvContent = """
            name,age
            Alice,30
            Bob,25
            Charlie,35
            """;
        var recipeYaml = """
            name: Filter age
            actions:
              - type: Filter
                columnName: age
                operator: GreaterThan
                comparisonType: Number
                value: 30
            """;
        var inputFile = _testDirectory.CreateFile("input.csv", csvContent);
        var recipeFile = _testDirectory.CreateFile("recipe.yaml", recipeYaml);
        var outputFile = Path.Combine(_testDirectory.Path, "output.csv");

        // Act
        var result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, _testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        var lines = await OutputFile.ReadLinesAsync(outputFile);
        lines.Should().Equal(
            "name,age", // header
            "Charlie,35"); // Bob,25 filtered out (age <= 30)
    }

    [Fact]
    public async Task Run_JsonLinesToCsv_WithFilterAction_ExitsWithZeroAndWritesHeaderAndMatchingRecordsOnly()
    {
        // Arrange
        var jsonLinesContent = """
            {"name":"Alice","age":30}
            {"name":"Bob","age":25}
            {"name":"Charlie","age":35}
            """;
        var recipeYaml = """
            name: Filter age
            actions:
              - type: Filter
                columnName: age
                operator: GreaterThan
                comparisonType: Number
                value: 30
            """;
        var inputFile = _testDirectory.CreateFile("input.jsonl", jsonLinesContent);
        var recipeFile = _testDirectory.CreateFile("recipe.yaml", recipeYaml);
        var outputFile = Path.Combine(_testDirectory.Path, "output.csv");

        // Act
        var result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, _testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        var lines = await OutputFile.ReadLinesAsync(outputFile);
        lines.Should().Equal(
            "name,age", // header
            "Charlie,35"); // Alice (30) and Bob (25) filtered out (age <= 30)
    }

    [Fact]
    public async Task Run_JsonLinesDrillDownToCsv_WithFilterAction_ExitsWithZeroAndWritesTheFilteredDrilledDownRows()
    {
        // Arrange — Full Aggregation: "orders" is traversed for every line; the Filter targets a
        // drilled-down column, not the base object.
        var jsonLinesContent = """
            {"orders":[{"id":1,"item":"a"}]}
            {"orders":[{"id":2,"item":"b"},{"id":3,"item":"c"}]}
            """;
        var recipeYaml = """
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
        var inputFile = _testDirectory.CreateFile("input.jsonl", jsonLinesContent);
        var recipeFile = _testDirectory.CreateFile("recipe.yaml", recipeYaml);
        var outputFile = Path.Combine(_testDirectory.Path, "output.csv");

        // Act
        var result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, _testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        var lines = await OutputFile.ReadLinesAsync(outputFile);
        lines.Should().Equal(
            "id,item", // header
            "2,b",
            "3,c"); // id 1 filtered out (id <= 1)
    }

    [Fact]
    public async Task Run_JsonArrayDrillDownToCsv_WithFilterAction_ExitsWithZeroAndWritesTheFilteredDrilledDownRows()
    {
        // Arrange — Full Aggregation: "orders" is traversed for every top-level array element.
        var jsonArrayContent = """
            [{"orders":[{"id":1,"item":"a"}]},{"orders":[{"id":2,"item":"b"},{"id":3,"item":"c"}]}]
            """;
        var recipeYaml = """
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
        var inputFile = _testDirectory.CreateFile("input.json", jsonArrayContent);
        var recipeFile = _testDirectory.CreateFile("recipe.yaml", recipeYaml);
        var outputFile = Path.Combine(_testDirectory.Path, "output.csv");

        // Act
        var result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, _testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        var lines = await OutputFile.ReadLinesAsync(outputFile);
        lines.Should().Equal(
            "id,item", // header
            "2,b",
            "3,c"); // id 1 filtered out (id <= 1)
    }

    [Fact]
    public async Task Run_JsonObjectDrillDownToCsv_WithFilterAction_ExitsWithZeroAndWritesTheFilteredDrilledDownRows()
    {
        // Arrange — Single DrillDown: "orders" names one node whose elements become the rows.
        var jsonObjectContent = """
            {"orders":[{"id":1,"item":"a"},{"id":2,"item":"b"},{"id":3,"item":"c"}]}
            """;
        var recipeYaml = """
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
        var inputFile = _testDirectory.CreateFile("input.json", jsonObjectContent);
        var recipeFile = _testDirectory.CreateFile("recipe.yaml", recipeYaml);
        var outputFile = Path.Combine(_testDirectory.Path, "output.csv");

        // Act
        var result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, _testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        var lines = await OutputFile.ReadLinesAsync(outputFile);
        lines.Should().Equal(
            "id,item", // header
            "2,b",
            "3,c"); // id 1 filtered out (id <= 1)
    }
}

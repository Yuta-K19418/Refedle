using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.JsonLines;

public sealed partial class JsonLinesOutputTests
{
    [Fact]
    public async Task Run_CsvToJsonLines_WithFilterAction_ExitsWithZeroAndWritesOnlyMatchingRecordsAsJsonObjects()
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
              - type: filter
                columnName: age
                operator: greaterThan
                comparisonType: Number
                value: 30
            """;
        var inputFile = _testDirectory.CreateFile("input.csv", csvContent);
        var recipeFile = _testDirectory.CreateFile("recipe.yaml", recipeYaml);
        var outputFile = Path.Combine(_testDirectory.Path, "output.jsonl");

        // Act
        var result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, _testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        var lines = await OutputFile.ReadLinesAsync(outputFile);
        // "35" is numeric CSV text, so it is written as a JSON number, not a quoted string.
        lines.Should().Equal(
            """{"name":"Charlie","age":35}"""); // Alice (30) and Bob (25) filtered out (age <= 30)
    }

    [Fact]
    public async Task Run_JsonLinesToJsonLines_WithFilterAction_ExitsWithZeroAndWritesOnlyMatchingRecords()
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
              - type: filter
                columnName: age
                operator: greaterThan
                comparisonType: Number
                value: 30
            """;
        var inputFile = _testDirectory.CreateFile("input.jsonl", jsonLinesContent);
        var recipeFile = _testDirectory.CreateFile("recipe.yaml", recipeYaml);
        var outputFile = Path.Combine(_testDirectory.Path, "output.jsonl");

        // Act
        var result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, _testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        var lines = await OutputFile.ReadLinesAsync(outputFile);
        lines.Should().Equal(
            """{"name":"Charlie","age":35}"""); // Alice (30) and Bob (25) filtered out (age <= 30)
    }
}

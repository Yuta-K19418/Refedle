using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.Csv;

public sealed partial class CsvOutputTests
{
    [Fact]
    public async Task Run_CsvToCsv_WithChainedActions_ExitsWithZeroAndWritesAllTransformationsInOrder()
    {
        // Arrange
        var csvContent = """
            name,age,email,joined
            Alice,30,,2026-01-15T10:30:00
            Bob,42,,2026-03-02T08:05:00
            Charlie,35,charlie@example.com,2026-05-20T18:45:00
            Diana,25,diana@example.com,2026-07-10T12:00:00
            """;
        var recipeYaml = """
            name: Chained transformations
            actions:
              - type: filter
                columnName: age
                operator: greaterThan
                comparisonType: Number
                value: 30
              - type: fill
                columnName: email
                value: "***"
              - type: format_timestamp
                columnName: joined
                targetFormat: yyyy-MM-dd
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
        // Filter keeps Bob (42) and Charlie (35); fill masks every remaining email cell;
        // timestamps are reformatted to the target format.
        lines.Should().Equal(
            "name,age,email,joined",
            "Bob,42,***,2026-03-02",
            "Charlie,35,***,2026-05-20");
    }
}

using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.Csv;

public sealed partial class CsvOutputTests
{
    private const string FilterActionRecipeYaml = """
        name: Filter age
        actions:
          - type: filter
            columnName: age
            operator: greaterThan
            comparisonType: Number
            value: 30
        """;

    [Fact]
    public async Task Run_CsvToCsv_WithFilterAction_ExitsWithZeroAndWritesOnlyMatchingRows()
    {
        // Arrange
        var inputFile = _testDirectory.CreateFile("input.csv", TestCsvContent);
        var recipeFile = _testDirectory.CreateFile("recipe.yaml", FilterActionRecipeYaml);
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
}

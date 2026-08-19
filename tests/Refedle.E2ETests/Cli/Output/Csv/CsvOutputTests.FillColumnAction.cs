using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.Csv;

public sealed partial class CsvOutputTests
{
    [Fact]
    public async Task Run_CsvToCsv_WithFillColumnAction_ExitsWithZeroAndWritesFillValueInEveryCellOfColumn()
    {
        // Arrange
        var csvContent = """
            name,email
            Alice,alice@example.com
            Bob,
            """;
        var recipeYaml = """
            name: Mask email
            actions:
              - type: Fill
                columnName: email
                value: "***"
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
        // FillColumnAction overwrites every cell of the column (masking semantics per its contract).
        lines.Should().Equal(
            "name,email",
            "Alice,***",
            "Bob,***");
    }
}

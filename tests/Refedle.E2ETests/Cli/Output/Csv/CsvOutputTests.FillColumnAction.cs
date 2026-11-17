using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.Csv;

public sealed partial class CsvOutputTests
{
    [Fact]
    public async Task Run_CsvToCsv_WithFillColumnAction_ExitsWithZeroAndWritesFillValueInEveryCellOfColumn()
    {
        // Arrange
        string csvContent = """
            name,email
            Alice,alice@example.com
            Bob,
            """;
        string recipeYaml = """
            name: Mask email
            actions:
              - type: Fill
                columnName: email
                value: "***"
            """;
        string inputFile = _testDirectory.CreateFile("input.csv", csvContent);
        string recipeFile = _testDirectory.CreateFile("recipe.yaml", recipeYaml);
        string outputFile = Path.Combine(_testDirectory.Path, "output.csv");

        // Act
        CliProcessResult result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, _testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        File.Exists(outputFile).Should().BeTrue();
        IReadOnlyList<string> lines = await OutputFile.ReadLinesAsync(outputFile);
        // FillColumnAction overwrites every cell of the column (masking semantics per its contract).
        lines.Should().Equal(
            "name,email",
            "Alice,***",
            "Bob,***");
    }
}

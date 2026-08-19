using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.Csv;

public sealed partial class CsvOutputTests
{
    [Fact]
    public async Task Run_CsvToCsv_WithRenameColumnAction_ExitsWithZeroAndWritesRenamedHeaderWithUnchangedValues()
    {
        // Arrange
        var csvContent = """
            name,age
            Alice,30
            Bob,25
            Charlie,35
            """;
        var recipeYaml = """
            name: Rename age
            actions:
              - type: Rename
                oldName: age
                newName: years
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
            "name,years", // header renamed; "age" is gone
            "Alice,30",
            "Bob,25",
            "Charlie,35");
    }
}

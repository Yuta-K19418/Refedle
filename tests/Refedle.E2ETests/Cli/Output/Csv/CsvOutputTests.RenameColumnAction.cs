using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.Csv;

public sealed partial class CsvOutputTests
{
    [Fact]
    public async Task Run_CsvToCsv_WithRenameColumnAction_ExitsWithZeroAndWritesRenamedHeaderWithUnchangedValues()
    {
        // Arrange
        string csvContent = """
            name,age
            Alice,30
            Bob,25
            Charlie,35
            """;
        string recipeYaml = """
            name: Rename age
            actions:
              - type: Rename
                oldName: age
                newName: years
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
        lines.Should().Equal(
            "name,years", // header renamed; "age" is gone
            "Alice,30",
            "Bob,25",
            "Charlie,35");
    }
}

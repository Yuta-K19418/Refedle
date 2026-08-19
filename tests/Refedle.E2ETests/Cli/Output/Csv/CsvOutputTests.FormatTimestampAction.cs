using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.Csv;

public sealed partial class CsvOutputTests
{
    [Fact]
    public async Task Run_CsvToCsv_WithFormatTimestampAction_ExitsWithZeroAndWritesReformattedTimestamps()
    {
        // Arrange
        var csvContent = """
            name,joined
            Alice,2026-01-15T10:30:00
            Bob,2026-03-02T08:05:00
            """;
        var recipeYaml = """
            name: Format joined
            actions:
              - type: FormatTimestamp
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
        lines.Should().Equal(
            "name,joined",
            "Alice,2026-01-15",
            "Bob,2026-03-02");
    }
}

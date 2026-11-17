using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.Csv;

public sealed partial class CsvOutputTests
{
    [Fact]
    public async Task Run_CsvToCsv_WithFormatTimestampAction_ExitsWithZeroAndWritesReformattedTimestamps()
    {
        // Arrange
        string csvContent = """
            name,joined
            Alice,2026-01-15T10:30:00
            Bob,2026-03-02T08:05:00
            """;
        string recipeYaml = """
            name: Format joined
            actions:
              - type: FormatTimestamp
                columnName: joined
                targetFormat: yyyy-MM-dd
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
            "name,joined",
            "Alice,2026-01-15",
            "Bob,2026-03-02");
    }
}

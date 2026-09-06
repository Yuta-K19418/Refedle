using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli;

public sealed class DryRunTests
{
    [Fact]
    public async Task Apply_WithDryRunWithoutOutput_PrintsSummaryToStdoutAndExitsZero()
    {
        // Arrange — dispatching to DryRunner is proven by the summary on stdout with no output file
        using var testDirectory = new TestDirectory();
        var inputFile = testDirectory.CreateFile("input.csv", "id,name\n1,Alice\n");
        var recipeFile = testDirectory.CreateFile("recipe.yaml", "name: Empty\nactions: []");

        // Act
        var result = await CliProcess.RunWithArgumentsAsync(
            ["apply", "--input", inputFile, "--recipe", recipeFile, "--dry-run"]);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        result.StandardOutput.Should().Contain("Dry run OK");
        result.StandardOutput.Should().Contain("  Input format: Csv");
        result.StandardOutput.Should().NotContain("  Output format:");
    }

    [Fact]
    public async Task Apply_WithDryRunWithOutput_DetectsFormatAndDoesNotWriteOutputFile()
    {
        // Arrange — a dry run never writes, even when --output is given
        using var testDirectory = new TestDirectory();
        var inputFile = testDirectory.CreateFile("input.csv", "id,name\n1,Alice\n");
        var recipeFile = testDirectory.CreateFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(testDirectory.Path, "output.jsonl");

        // Act
        var result = await CliProcess.RunWithArgumentsAsync(
            ["apply", "--input", inputFile, "--recipe", recipeFile, "--output", outputFile, "--dry-run"]);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        result.StandardOutput.Should().Contain("Dry run OK");
        result.StandardOutput.Should().Contain("  Output format: JsonLines");
        File.Exists(outputFile).Should().BeFalse();
    }
}

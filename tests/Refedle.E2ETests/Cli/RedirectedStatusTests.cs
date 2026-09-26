using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli;

// The child process always has redirected stdout, so these prove Spectre's non-interactive
// fallback: plain phase text and no terminal control sequences.
public sealed class RedirectedStatusTests
{
    private const string EscapeCharacter = "\u001b";

    [Fact]
    public async Task Apply_CsvToCsv_WithRedirectedStdout_PrintsPlainPhaseNameWithoutEscapeSequences()
    {
        // Arrange — "Preparing..." can be superseded before it is drawn, so either phase name is accepted
        using var testDirectory = new TestDirectory();
        var inputFile = testDirectory.CreateFile("input.csv", "id,name\n1,Alice\n");
        var recipeFile = testDirectory.CreateFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(testDirectory.Path, "output.csv");

        // Act
        var result = await CliProcess.RunAsync(inputFile, recipeFile, outputFile, testDirectory.Path);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().MatchRegex(@"(Preparing|Converting)\.\.\.");
        result.StandardOutput.Should().NotContain(EscapeCharacter);
        result.StandardError.Should().NotContain(EscapeCharacter);
    }

    [Fact]
    public async Task Apply_WithDryRunWithoutOutputAndRedirectedStdout_PrintsValidatingWithoutEscapeSequences()
    {
        // Arrange
        using var testDirectory = new TestDirectory();
        var inputFile = testDirectory.CreateFile("input.csv", "id,name\n1,Alice\n");
        var recipeFile = testDirectory.CreateFile("recipe.yaml", "name: Empty\nactions: []");

        // Act
        var result = await CliProcess.RunWithArgumentsAsync(
            ["apply", "--input", inputFile, "--recipe", recipeFile, "--dry-run"]);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Contain("Validating...");
        result.StandardOutput.Should().Contain("Dry run OK");
        result.StandardOutput.Should().NotContain(EscapeCharacter);
        result.StandardError.Should().NotContain(EscapeCharacter);
    }
}

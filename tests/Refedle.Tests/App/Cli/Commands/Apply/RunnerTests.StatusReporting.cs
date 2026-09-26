using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.App.Cli.Commands.Apply;
using Refedle.App.Cli.Parsing;

namespace Refedle.Tests.App.Cli.Commands.Apply;

public sealed partial class RunnerTests
{
    [Fact]
    public async Task RunAsync_OnSuccess_ReportsPreparingThenConvertingPhases()
    {
        // Arrange
        var args = CreateStatusArgs(TestRecipeYaml);
        var logger = new TestAppLogger();
        var status = new TestStatusReporter();

        // Act
        var exitCode = await Runner.RunAsync(
            args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), status);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        status.Phases.Should().Equal("Preparing...", "Converting...");
    }

    [Fact]
    public async Task RunAsync_WhenPreparationFails_ReportsOnlyPreparingAndStillLogsTheError()
    {
        // Arrange
        var args = CreateStatusArgs(TestRecipeYaml) with { RecipeFile = Path.Combine(_testDir, "missing.yaml") };
        var logger = new TestAppLogger();
        var status = new TestStatusReporter();

        // Act
        var exitCode = await Runner.RunAsync(
            args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), status);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        status.Phases.Should().Equal("Preparing...");
        logger.Errors.Should().ContainSingle().Which.Should().Contain("missing.yaml");
    }

    [Fact]
    public async Task RunAsync_WithNullStatusReporter_ThrowsArgumentNullException()
    {
        // Arrange
        var args = CreateStatusArgs(TestRecipeYaml);
        var logger = new TestAppLogger();

        // Act
        var act = async () => await Runner.RunAsync(
            args, logger, new GeneratedFormatDispatcher(), new TempOutputPathProvider(), null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private Arguments CreateStatusArgs(string recipeYaml)
    {
        var inputFile = Path.Combine(_testDir, "status-input.csv");
        var recipeFile = Path.Combine(_testDir, "status-recipe.yaml");
        File.WriteAllText(inputFile, TestCsvContent);
        File.WriteAllText(recipeFile, recipeYaml);
        return new Arguments
        {
            InputFile = inputFile,
            RecipeFile = recipeFile,
            OutputFile = Path.Combine(_testDir, "status-output.csv"),
        };
    }
}

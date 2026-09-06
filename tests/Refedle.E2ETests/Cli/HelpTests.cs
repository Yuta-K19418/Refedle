using AwesomeAssertions;
using Refedle.App;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli;

public sealed class HelpTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public async Task Run_WithHelpArgument_PrintsHelpTextAndExitsWithZero(string argument)
    {
        // Arrange

        // Act
        var result = await CliProcess.RunWithArgumentsAsync([argument]);

        // Assert
        AssertHelpOutput(result);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task Run_WithApplyModeAndHelpFlag_PrintsHelpTextAndExitsWithZero(string helpFlag)
    {
        // Arrange
        string[] arguments = ["apply", helpFlag];

        // Act
        var result = await CliProcess.RunWithArgumentsAsync(arguments);

        // Assert
        AssertHelpOutput(result);
    }

    [Fact]
    public async Task Run_WithVersionAndHelpFlags_PrintsHelpTextAndExitsWithZero()
    {
        // Arrange
        string[] arguments = ["--version", "--help"];

        // Act
        var result = await CliProcess.RunWithArgumentsAsync(arguments);

        // Assert
        AssertHelpOutput(result);
        result.StandardOutput.Should().NotContain($"refedle {BuildInfo.Version}");
    }

    private static void AssertHelpOutput(CliProcessResult result)
    {
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        result.StandardOutput.Should().Contain("Usage:");
        result.StandardOutput.Should().Contain("Commands:");
        result.StandardOutput.Should().Contain("Options:");
        result.StandardOutput.Should().Contain("apply");
        result.StandardOutput.Should().Contain("update");
        result.StandardOutput.Should().Contain("version");
        result.StandardOutput.Should().Contain("help");
        result.StandardOutput.Should().Contain("--file <path>");
        result.StandardOutput.Should().Contain("--recipe <path>");
        result.StandardOutput.Should().Contain("--version");
        result.StandardOutput.Should().Contain("--help, -h");
    }
}

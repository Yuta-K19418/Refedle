using AwesomeAssertions;
using Refedle.App;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli;

public sealed class VersionTests
{
    [Theory]
    [InlineData("--version")]
    [InlineData("version")]
    public async Task Run_WithVersionArgument_PrintsVersionLineAndExitsWithZero(string argument)
    {
        // Arrange

        // Act
        var result = await CliProcess.RunWithArgumentsAsync([argument]);

        // Assert
        AssertVersionOutput(result);
    }

    [Fact]
    public async Task Run_WithVersionSubcommandAndExtraArgument_PrintsVersionLineAndExitsWithZero()
    {
        // Arrange
        string[] arguments = ["version", "ignored"];

        // Act
        var result = await CliProcess.RunWithArgumentsAsync(arguments);

        // Assert
        AssertVersionOutput(result);
    }

    [Fact]
    public async Task Run_WithCliModeAndVersionFlag_PrintsVersionLineAndExitsWithZero()
    {
        // Arrange
        string[] arguments = ["--cli", "--version"];

        // Act
        var result = await CliProcess.RunWithArgumentsAsync(arguments);

        // Assert
        AssertVersionOutput(result);
    }

    [Fact]
    public async Task Run_WithVersionSubcommandAfterCliMode_ExitsWithNonZeroWithoutVersionLine()
    {
        // Arrange
        string[] arguments = ["--cli", "version"];

        // Act
        var result = await CliProcess.RunWithArgumentsAsync(arguments);

        // Assert
        result.ExitCode.Should().Be(1);
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().Be($"Invalid flag: 'version'{Environment.NewLine}");
    }

    private static void AssertVersionOutput(CliProcessResult result)
    {
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        result.StandardOutput.Should().Be($"refedle {BuildInfo.Version}{Environment.NewLine}");
    }
}

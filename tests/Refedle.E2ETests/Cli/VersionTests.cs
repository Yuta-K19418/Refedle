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
    public async Task Run_WithApplyModeAndVersionFlag_PrintsVersionLineAndExitsWithZero()
    {
        // Arrange
        string[] arguments = ["apply", "--version"];

        // Act
        var result = await CliProcess.RunWithArgumentsAsync(arguments);

        // Assert
        AssertVersionOutput(result);
    }

    private static void AssertVersionOutput(CliProcessResult result)
    {
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        result.StandardOutput.Should().Be($"refedle {BuildInfo.Version}{Environment.NewLine}");
    }
}

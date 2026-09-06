using AwesomeAssertions;
using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

public sealed class VersionCommandTests
{
    [Theory]
    [InlineData(true, "--version")]
    [InlineData(true, "version")]
    [InlineData(true, "version", "ignored")]
    [InlineData(true, "apply", "--version")]
    [InlineData(false, "apply", "version")]
    [InlineData(false, "input.csv", "version")]
    [InlineData(false)]
    public void IsMatch_WithArguments_ReturnsExpectedDispatchDecision(bool expected, params string[] arguments)
    {
        // Arrange

        // Act
        var isMatch = VersionCommand.IsMatch(arguments);

        // Assert
        isMatch.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("0.0.0-dev")]
    [InlineData("10.20.30")]
    public async Task RunAsync_WithVersion_WritesSingleVersionLineAndReturnsSuccess(string version)
    {
        // Arrange
        var logger = new TestAppLogger();
        var command = new VersionCommand(version, logger);

        // Act
        var exitCode = await command.RunAsync();

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Infos.Should().Equal($"refedle {version}");
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }
}

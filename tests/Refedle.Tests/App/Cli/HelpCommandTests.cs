using AwesomeAssertions;
using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

public sealed class HelpCommandTests
{
    [Theory]
    [InlineData(true, "--help")]
    [InlineData(true, "-h")]
    [InlineData(true, "help")]
    [InlineData(true, "help", "ignored")]
    [InlineData(true, "apply", "--help")]
    [InlineData(true, "apply", "-h")]
    [InlineData(false, "apply", "help")]
    [InlineData(false, "input.csv", "help")]
    [InlineData(false)]
    public void IsMatch_WithArguments_ReturnsExpectedDispatchDecision(bool expected, params string[] arguments)
    {
        // Arrange

        // Act
        var isMatch = HelpCommand.IsMatch(arguments);

        // Assert
        isMatch.Should().Be(expected);
    }

    [Theory]
    [InlineData("Usage:")]
    [InlineData("Commands:")]
    [InlineData("Options:")]
    [InlineData("apply")]
    [InlineData("update")]
    [InlineData("version")]
    [InlineData("help")]
    [InlineData("--file <path>")]
    [InlineData("--recipe <path>")]
    [InlineData("--version")]
    [InlineData("--help, -h")]
    public async Task RunAsync_WithAnyState_WritesAllHelpSectionsAndEntriesAndReturnsSuccess(string expectedFragment)
    {
        // Arrange
        var logger = new TestAppLogger();
        var command = new HelpCommand(logger);

        // Act
        var exitCode = await command.RunAsync();

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        logger.Infos.Should().ContainSingle().Which.Should().Contain(expectedFragment);
        logger.Warnings.Should().BeEmpty();
        logger.Errors.Should().BeEmpty();
    }
}

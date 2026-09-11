using AwesomeAssertions;
using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

public sealed class CliCommandMatcherTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    [InlineData("help", "ignored")]
    [InlineData("apply", "--help")]
    [InlineData("--help", "--version")]
    public void TryMatch_WithHelpArguments_ReturnsTrueAndHelp(params string[] arguments)
    {
        // Arrange

        // Act
        var isMatch = CliCommandMatcher.TryMatch(arguments, out var command);

        // Assert
        isMatch.Should().BeTrue();
        command.Should().Be(CliCommand.Help);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("version")]
    [InlineData("apply", "--version")]
    public void TryMatch_WithVersionArguments_ReturnsTrueAndVersion(params string[] arguments)
    {
        // Arrange

        // Act
        var isMatch = CliCommandMatcher.TryMatch(arguments, out var command);

        // Assert
        isMatch.Should().BeTrue();
        command.Should().Be(CliCommand.Version);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("update", "--foo")]
    public void TryMatch_WithUpdateArguments_ReturnsTrueAndUpdate(params string[] arguments)
    {
        // Arrange

        // Act
        var isMatch = CliCommandMatcher.TryMatch(arguments, out var command);

        // Assert
        isMatch.Should().BeTrue();
        command.Should().Be(CliCommand.Update);
    }

    [Theory]
    [InlineData("apply")]
    [InlineData("apply", "--input", "in.csv")]
    public void TryMatch_WithApplyArguments_ReturnsTrueAndApply(params string[] arguments)
    {
        // Arrange

        // Act
        var isMatch = CliCommandMatcher.TryMatch(arguments, out var command);

        // Assert
        isMatch.Should().BeTrue();
        command.Should().Be(CliCommand.Apply);
    }

    [Theory]
    [InlineData("input.csv")]
    [InlineData("input.csv", "help")]
    [InlineData("input.csv", "version")]
    [InlineData()]
    public void TryMatch_WithNonMatchingArguments_ReturnsFalse(params string[] arguments)
    {
        // Arrange

        // Act
        var isMatch = CliCommandMatcher.TryMatch(arguments, out _);

        // Assert
        isMatch.Should().BeFalse();
    }
}

using AwesomeAssertions;
using Refedle.App.Cli.Commands.Update;

namespace Refedle.Tests.App.Cli.Commands.Update;

public sealed class ChecksumsTests
{
    private static readonly string _hexA = new('a', 64);
    private static readonly string _hexB = new('b', 64);

    [Fact]
    public void FindHex_WithMatchingEntry_ReturnsUpperCaseHex()
    {
        // Arrange
        var content = $"{_hexA}  refedle-v0.3.0-linux-x64.tar.gz\n{_hexB}  refedle-v0.3.0-osx-arm64.tar.gz\n";

        // Act
        var result = Checksums.FindHex(content, "refedle-v0.3.0-osx-arm64.tar.gz");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_hexB.ToUpperInvariant());
    }

    [Fact]
    public void FindHex_WithUpperCaseHexInFile_NormalizesToUpperCase()
    {
        // Arrange
        var content = $"{_hexA.ToUpperInvariant()}  refedle.tar.gz\n";

        // Act
        var result = Checksums.FindHex(content, "refedle.tar.gz");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_hexA.ToUpperInvariant());
    }

    [Fact]
    public void FindHex_WithCrlfLineEndings_MatchesEntry()
    {
        // Arrange
        var content = $"{_hexA}  other.tar.gz\r\n{_hexB}  refedle.tar.gz\r\n";

        // Act
        var result = Checksums.FindHex(content, "refedle.tar.gz");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_hexB.ToUpperInvariant());
    }

    [Fact]
    public void FindHex_WithBinaryMarkerPrefix_MatchesFileName()
    {
        // Arrange
        var content = $"{_hexA} *refedle.tar.gz\n";

        // Act
        var result = Checksums.FindHex(content, "refedle.tar.gz");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_hexA.ToUpperInvariant());
    }

    [Fact]
    public void FindHex_WhenFileNameIsAbsent_FailsWithNotFoundMessage()
    {
        // Arrange
        var content = $"{_hexA}  refedle-v0.3.0-linux-x64.tar.gz\n";

        // Act
        var result = Checksums.FindHex(content, "refedle-v0.3.0-linux-arm64.tar.gz");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No checksum entry for 'refedle-v0.3.0-linux-arm64.tar.gz'");
    }

    [Theory]
    [InlineData("this is not a checksums line")]
    [InlineData("abc  refedle.tar.gz")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg  refedle.tar.gz")]
    public void FindHex_WithMalformedLine_FailsWithInvalidLineMessage(string malformedLine)
    {
        // Arrange
        var content = $"{malformedLine}\n";

        // Act
        var result = Checksums.FindHex(content, "refedle.tar.gz");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid checksums line");
    }

    [Fact]
    public void FindHex_WithBlankLinesAroundEntry_IgnoresBlankLines()
    {
        // Arrange
        var content = $"\n\n{_hexA}  refedle.tar.gz\n\n";

        // Act
        var result = Checksums.FindHex(content, "refedle.tar.gz");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_hexA.ToUpperInvariant());
    }
}

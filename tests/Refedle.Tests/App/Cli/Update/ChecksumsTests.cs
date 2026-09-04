using AwesomeAssertions;
using Refedle.App.Cli.Update;

namespace Refedle.Tests.App.Cli.Update;

public sealed class ChecksumsTests
{
    private static readonly string HexA = new('a', 64);
    private static readonly string HexB = new('b', 64);

    [Fact]
    public void FindHex_WithMatchingEntry_ReturnsUpperCaseHex()
    {
        // Arrange
        var content = $"{HexA}  refedle-v0.3.0-linux-x64.tar.gz\n{HexB}  refedle-v0.3.0-osx-arm64.tar.gz\n";

        // Act
        var result = Checksums.FindHex(content, "refedle-v0.3.0-osx-arm64.tar.gz");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(HexB.ToUpperInvariant());
    }

    [Fact]
    public void FindHex_WithUpperCaseHexInFile_NormalizesToUpperCase()
    {
        // Arrange
        var content = $"{HexA.ToUpperInvariant()}  refedle.tar.gz\n";

        // Act
        var result = Checksums.FindHex(content, "refedle.tar.gz");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(HexA.ToUpperInvariant());
    }

    [Fact]
    public void FindHex_WithCrlfLineEndings_MatchesEntry()
    {
        // Arrange
        var content = $"{HexA}  other.tar.gz\r\n{HexB}  refedle.tar.gz\r\n";

        // Act
        var result = Checksums.FindHex(content, "refedle.tar.gz");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(HexB.ToUpperInvariant());
    }

    [Fact]
    public void FindHex_WithBinaryMarkerPrefix_MatchesFileName()
    {
        // Arrange
        var content = $"{HexA} *refedle.tar.gz\n";

        // Act
        var result = Checksums.FindHex(content, "refedle.tar.gz");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(HexA.ToUpperInvariant());
    }

    [Fact]
    public void FindHex_WhenFileNameIsAbsent_FailsWithNotFoundMessage()
    {
        // Arrange
        var content = $"{HexA}  refedle-v0.3.0-linux-x64.tar.gz\n";

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
        var content = $"\n\n{HexA}  refedle.tar.gz\n\n";

        // Act
        var result = Checksums.FindHex(content, "refedle.tar.gz");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(HexA.ToUpperInvariant());
    }
}

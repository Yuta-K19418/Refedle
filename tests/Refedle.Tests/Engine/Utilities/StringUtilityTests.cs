using System.Text;
using AwesomeAssertions;
using Refedle.Engine.Utilities;

namespace Refedle.Tests.Engine.Utilities;

public sealed class StringUtilityTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData(" ", true)]
    [InlineData("\t", true)]
    [InlineData("\r", true)]
    [InlineData("\n", true)]
    [InlineData(" \t\r\n", true)]
    [InlineData("a", false)]
    [InlineData(" value", false)]
    [InlineData(" \ta", false)]
    [InlineData("a\n", false)]
    public void IsWhiteSpace_WithAsciiBytes_ReturnsExpectedResult(string value, bool expected)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(value);

        // Act
        var result = StringUtility.IsWhiteSpace(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("", "\"\"")]
    [InlineData("value", "\"value\"")]
    [InlineData("has \"quotes\"", "\"has \\\"quotes\\\"\"")]
    [InlineData(@"back\slash", "\"back\\\\slash\"")]
    public void QuoteString_WithVariousInputs_ReturnsQuotedResult(string value, string expected)
    {
        // Arrange

        // Act
        var result = StringUtility.QuoteString(value);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void QuoteString_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        string value = null!;

        // Act
        Action action = () => StringUtility.QuoteString(value);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("value");
    }

    [Theory]
    [InlineData("\"\"", "")]
    [InlineData("\"value\"", "value")]
    [InlineData("\"has \\\"quotes\\\"\"", "has \"quotes\"")]
    [InlineData("\"back\\\\slash\"", @"back\slash")]
    [InlineData("unquoted", "unquoted")]
    [InlineData("\"", "\"")]
    public void UnquoteString_WithVariousInputs_ReturnsUnquotedResult(string value, string expected)
    {
        // Arrange

        // Act
        var result = StringUtility.UnquoteString(value);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("\"value\\x\"", "value\\x")]
    [InlineData("\"value\\\"", "value\\")]
    public void UnquoteString_WithNonStandardOrTerminalEscape_ReturnsBackslashPreservingResult(string value, string expected)
    {
        // Arrange

        // Act
        var result = StringUtility.UnquoteString(value);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void UnquoteString_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        string value = null!;

        // Act
        Action action = () => StringUtility.UnquoteString(value);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("value");
    }

    [Theory]
    [InlineData("")]
    [InlineData("value")]
    [InlineData("has \"quotes\"")]
    [InlineData(@"back\slash")]
    public void QuoteString_ThenUnquoteString_RoundTrips(string value)
    {
        // Arrange

        // Act
        var result = StringUtility.UnquoteString(StringUtility.QuoteString(value));

        // Assert
        result.Should().Be(value);
    }
}

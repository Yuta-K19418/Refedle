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
}

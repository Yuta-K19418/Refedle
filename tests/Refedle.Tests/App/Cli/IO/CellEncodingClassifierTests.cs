using AwesomeAssertions;
using Refedle.App.Cli.IO;

namespace Refedle.Tests.App.Cli.IO;

public sealed class CellEncodingClassifierTests
{
    [Theory]
    [InlineData("007", CellEncoding.Numeric)]
    [InlineData("1,234", CellEncoding.Numeric)]
    [InlineData("TRUE", CellEncoding.Boolean)]
    [InlineData("plain text", CellEncoding.PlainText)]
    internal void Classify_VariousText_ReturnsExpectedEncoding(string input, CellEncoding expectedEncoding)
    {
        // Arrange

        // Act
        var actual = CellEncodingClassifier.Classify(input);

        // Assert
        actual.Should().Be(expectedEncoding);
    }
}

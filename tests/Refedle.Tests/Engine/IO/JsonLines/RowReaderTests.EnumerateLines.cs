using System.Text;
using AwesomeAssertions;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed partial class RowReaderTests
{
    private static string[] DecodeLines(IEnumerable<JsonRawBytes> lines)
    {
        return [.. lines.Select(static line => Encoding.UTF8.GetString(line.Span))];
    }

    [Fact]
    public void EnumerateLines_WithMultipleLines_ReturnsAllLinesInOrder()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n{\"b\":2}\n{\"c\":3}\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.EnumerateLines(linesToSkip: 0));

        // Assert
        lines.Should().Equal("{\"a\":1}", "{\"b\":2}", "{\"c\":3}");
    }

    [Fact]
    public void EnumerateLines_WhenFileHasBom_ExcludesBomFromFirstLine()
    {
        // Arrange
        WriteTestContent(
            "{\"id\":1}\n{\"id\":2}\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.EnumerateLines(linesToSkip: 0));

        // Assert
        lines.Should().Equal("{\"id\":1}", "{\"id\":2}");
    }

    [Fact]
    public void EnumerateLines_WhenFileHasBomAndLinesToSkip_ReturnsLinesAfterSkippedOnes()
    {
        // Arrange
        WriteTestContent(
            "{\"id\":1}\n{\"id\":2}\n{\"id\":3}\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.EnumerateLines(linesToSkip: 1));

        // Assert
        lines.Should().Equal("{\"id\":2}", "{\"id\":3}");
    }

    [Fact]
    public void EnumerateLines_WithLinesToSkip_ReturnsOnlyLinesAfterSkippedOnes()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n{\"b\":2}\n{\"c\":3}\n{\"d\":4}\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.EnumerateLines(linesToSkip: 2));

        // Assert
        lines.Should().Equal("{\"c\":3}", "{\"d\":4}");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void EnumerateLines_WithNewlineStyle_ReturnsLinesWithoutNewlineCharacters(string newline)
    {
        // Arrange
        WriteTestContent($"{{\"a\":1}}{newline}{{\"b\":2}}{newline}");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.EnumerateLines(linesToSkip: 0));

        // Assert
        lines.Should().Equal("{\"a\":1}", "{\"b\":2}");
    }

    [Fact]
    public void EnumerateLines_WhenLastLineHasNoTrailingNewline_ReturnsLastLine()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n{\"b\":2}");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.EnumerateLines(linesToSkip: 0));

        // Assert
        lines.Should().Equal("{\"a\":1}", "{\"b\":2}");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void EnumerateLines_WhenLinesToSkipIsAtLeastLineCount_ReturnsEmpty(int linesToSkip)
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n{\"b\":2}\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.EnumerateLines(linesToSkip));

        // Assert
        lines.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateLines_WhenReaderDisposedDuringEnumeration_ThrowsObjectDisposedException()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n{\"b\":2}\n");
        var reader = new RowReader(_testFilePath);
        using var enumerator = reader.EnumerateLines(linesToSkip: 0).GetEnumerator();
        var hasFirstLine = enumerator.MoveNext();
        reader.Dispose();

        // Act
        var act = () => enumerator.MoveNext();

        // Assert
        hasFirstLine.Should().BeTrue();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void EnumerateLines_WhenReaderDisposedBeforeEnumeration_ThrowsObjectDisposedException()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n");
        var reader = new RowReader(_testFilePath);
        var lines = reader.EnumerateLines(linesToSkip: 0);
        reader.Dispose();

        // Act
        var act = () => DecodeLines(lines);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }
}

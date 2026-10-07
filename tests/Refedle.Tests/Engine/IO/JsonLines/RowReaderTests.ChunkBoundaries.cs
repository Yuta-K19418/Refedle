using AwesomeAssertions;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed partial class RowReaderTests
{
    // Line lengths vary so that chunk boundaries fall at different positions inside different lines.
    private static string[] BuildVariedLengthLines(int count)
    {
        return [.. Enumerable.Range(0, count).Select(static i => $"{{\"id\":{i},\"pad\":\"{new string('x', 50 + (i * 7 % 311))}\"}}")];
    }

    private static string[] BuildLongAndShortLines()
    {
        return
        [
            $"{{\"long\":\"{new string('a', 200_000)}\"}}",
            "{\"short\":1}",
            $"{{\"long\":\"{new string('b', 300_000)}\"}}",
            "{\"short\":2}",
        ];
    }

    [Fact]
    public void ReadLines_WithFileSpanningManyChunks_ReturnsEveryLineIntact()
    {
        // Arrange
        var expectedLines = BuildVariedLengthLines(count: 3_000);
        WriteTestContent(string.Join('\n', expectedLines) + "\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: expectedLines.Length));

        // Assert
        lines.Should().Equal(expectedLines);
    }

    [Fact]
    public void ReadLines_WithLinesToSkipSpanningManyChunks_ReturnsLinesAfterSkippedOnes()
    {
        // Arrange
        var allLines = BuildVariedLengthLines(count: 3_000);
        WriteTestContent(string.Join('\n', allLines) + "\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.ReadLines(byteOffset: 0, linesToSkip: 2_500, linesToRead: 10));

        // Assert
        lines.Should().Equal(allLines.Skip(2_500).Take(10));
    }

    [Fact]
    public void ReadLines_WithLinesToSkipEqualToLineCountOfLargeFile_ReturnsEmptyList()
    {
        // Arrange
        var allLines = BuildVariedLengthLines(count: 3_000);
        WriteTestContent(string.Join('\n', allLines) + "\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: allLines.Length + 1, linesToRead: 1);

        // Assert
        lines.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ReadLines_WithLinesLongerThanOneChunk_ReturnsRemainingLinesIntact(int linesToSkip)
    {
        // Arrange
        var allLines = BuildLongAndShortLines();
        WriteTestContent(string.Join('\n', allLines) + "\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.ReadLines(byteOffset: 0, linesToSkip: linesToSkip, linesToRead: allLines.Length));

        // Assert
        lines.Should().Equal(allLines.Skip(linesToSkip));
    }

    [Fact]
    public void ReadLines_WhenLastLineWithoutNewlineIsLongerThanOneChunk_ReturnsLastLineIntact()
    {
        // Arrange
        var allLines = new[] { "{\"short\":1}", $"{{\"long\":\"{new string('c', 250_000)}\"}}" };
        WriteTestContent(string.Join('\n', allLines));
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 2));

        // Assert
        lines.Should().Equal(allLines);
    }

    [Fact]
    public void ReadLines_WhenSkippingPastLastLineWithoutNewlineLongerThanOneChunk_ReturnsEmptyList()
    {
        // Arrange
        var allLines = new[] { "{\"short\":1}", $"{{\"long\":\"{new string('c', 250_000)}\"}}" };
        WriteTestContent(string.Join('\n', allLines));
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 2, linesToRead: 1);

        // Assert
        lines.Should().BeEmpty();
    }

    [Theory]
    [InlineData(65_534)]
    [InlineData(65_535)]
    [InlineData(65_536)]
    [InlineData(65_537)]
    [InlineData(65_538)]
    public void ReadLines_WithCrLfLineEndingsNearChunkSizeLengths_TrimsLineEndingsFromEveryLine(int firstLineLength)
    {
        // Arrange
        var allLines = new[]
        {
            new string('a', firstLineLength),
            new string('b', 100),
            new string('c', 100),
        };
        WriteTestContent(string.Join("\r\n", allLines) + "\r\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: allLines.Length));

        // Assert
        lines.Should().Equal(allLines);
    }

    [Fact]
    public void ReadLines_WithLinesToSkipWhenCrLfIsSplitAcrossChunks_ReturnsLinesAfterSkippedOne()
    {
        // Arrange
        // The first line is 65,535 bytes, so its \r is the last byte of a 64 KB chunk and its \n starts the next one
        var allLines = new[]
        {
            new string('a', 65_535),
            new string('b', 100),
            new string('c', 100),
        };
        WriteTestContent(string.Join("\r\n", allLines) + "\r\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.ReadLines(byteOffset: 0, linesToSkip: 1, linesToRead: 2));

        // Assert
        lines.Should().Equal(allLines.Skip(1));
    }

    [Fact]
    public void EnumerateLines_WhenCrLfIsSplitAcrossChunks_TrimsLineEndingsFromEveryLine()
    {
        // Arrange
        // The first line is 65,535 bytes, so its \r is the last byte of a 64 KB chunk and its \n starts the next one
        var allLines = new[]
        {
            new string('a', 65_535),
            new string('b', 100),
            new string('c', 100),
        };
        WriteTestContent(string.Join("\r\n", allLines) + "\r\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.EnumerateLines(linesToSkip: 0));

        // Assert
        lines.Should().Equal(allLines);
    }

    [Fact]
    public void EnumerateLines_WithFileSpanningManyChunks_ReturnsEveryLineIntact()
    {
        // Arrange
        var expectedLines = BuildVariedLengthLines(count: 3_000);
        WriteTestContent(string.Join('\n', expectedLines) + "\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.EnumerateLines(linesToSkip: 0));

        // Assert
        lines.Should().Equal(expectedLines);
    }

    [Fact]
    public void EnumerateLines_WithLinesLongerThanOneChunk_ReturnsLinesAfterSkippedOnesIntact()
    {
        // Arrange
        var allLines = BuildLongAndShortLines();
        WriteTestContent(string.Join('\n', allLines) + "\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = DecodeLines(reader.EnumerateLines(linesToSkip: 1));

        // Assert
        lines.Should().Equal(allLines.Skip(1));
    }
}

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
}

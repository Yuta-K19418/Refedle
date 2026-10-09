using System.Text;
using AwesomeAssertions;
using Refedle.Engine.IO;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed class LineChunkCursorTests : IDisposable
{
    private readonly string _testFilePath = Path.Combine(Path.GetTempPath(), $"line_chunk_cursor_tests_{Guid.NewGuid()}.jsonl");
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            File.Delete(_testFilePath);
            _disposed = true;
        }
    }

    private MmapService OpenMmap(string content)
    {
        File.WriteAllText(_testFilePath, content, new UTF8Encoding(false));
        var openResult = MmapService.Open(_testFilePath);
        return openResult.Value;
    }

    private static string[] ReadRemainingLines(LineChunkCursor cursor)
    {
        List<string> lines = [];
        while (cursor.TryReadLine(out var line))
        {
            lines.Add(Encoding.UTF8.GetString(line.Span));
        }

        return [.. lines];
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

    // The first line is 65,535 bytes, so its \r is the last byte of a 64 KB chunk and its \n starts the next one
    private static string[] BuildLinesWithCrLfSplitAcrossChunks()
    {
        return [new string('a', 65_535), new string('b', 100), new string('c', 100)];
    }

    private static string[] BuildManyShortLines()
    {
        // About 300 KB in total, so the lines span several 64 KB chunks
        return [.. Enumerable.Range(0, 20_000).Select(i => $"{{\"i\":{i}}}")];
    }

    private static int SkipLines(LineChunkCursor cursor, int count)
    {
        var skippedCount = 0;
        while (skippedCount < count && cursor.TrySkipLine())
        {
            skippedCount++;
        }

        return skippedCount;
    }

    // TrySkipLine

    [Fact]
    public void TrySkipLine_WithShortLinesSpanningMultipleChunks_NextReadReturnsFollowingLine()
    {
        // Arrange
        var lines = BuildManyShortLines();
        using var mmap = OpenMmap(string.Join('\n', lines) + "\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var skippedCount = SkipLines(cursor, 15_000);
        var remainingLines = ReadRemainingLines(cursor);

        // Assert
        skippedCount.Should().Be(15_000);
        remainingLines.Should().Equal(lines.Skip(15_000));
    }

    [Fact]
    public void TrySkipLine_WithShortLinesSpanningMultipleChunks_ReturnsFalseAfterSkippingEveryLine()
    {
        // Arrange
        var lines = BuildManyShortLines();
        using var mmap = OpenMmap(string.Join('\n', lines) + "\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var skippedCount = SkipLines(cursor, lines.Length + 1);

        // Assert
        skippedCount.Should().Be(lines.Length);
    }

    [Fact]
    public void TrySkipLine_AtEndOfFile_ReturnsFalse()
    {
        // Arrange
        const string content = "{\"a\":1}\n";
        using var mmap = OpenMmap(content);
        using var cursor = new LineChunkCursor(mmap, startOffset: content.Length);

        // Act
        var skipped = cursor.TrySkipLine();

        // Assert
        skipped.Should().BeFalse();
    }

    [Fact]
    public void TrySkipLine_WithTerminatedLine_ReturnsTrueAndNextReadReturnsFollowingLine()
    {
        // Arrange
        using var mmap = OpenMmap("{\"a\":1}\n{\"b\":2}\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var skipped = cursor.TrySkipLine();
        var hasLine = cursor.TryReadLine(out var line);

        // Assert
        skipped.Should().BeTrue();
        hasLine.Should().BeTrue();
        Encoding.UTF8.GetString(line.Span).Should().Be("{\"b\":2}");
    }

    [Fact]
    public void TrySkipLine_WithEmptyLine_ReturnsTrueAndNextReadReturnsFollowingLine()
    {
        // Arrange
        using var mmap = OpenMmap("\n{\"b\":2}\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var skipped = cursor.TrySkipLine();
        var hasLine = cursor.TryReadLine(out var line);

        // Assert
        skipped.Should().BeTrue();
        hasLine.Should().BeTrue();
        Encoding.UTF8.GetString(line.Span).Should().Be("{\"b\":2}");
    }

    [Fact]
    public void TrySkipLine_WhenLastLineHasNoNewline_ReturnsFalse()
    {
        // Arrange
        using var mmap = OpenMmap("{\"a\":1}");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var skipped = cursor.TrySkipLine();

        // Assert
        skipped.Should().BeFalse();
    }

    [Fact]
    public void TrySkipLine_WithLineLongerThanOneChunk_ReturnsTrueAndNextReadReturnsFollowingLine()
    {
        // Arrange
        var lines = BuildLongAndShortLines();
        using var mmap = OpenMmap(string.Join('\n', lines) + "\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var skipped = cursor.TrySkipLine();
        var remainingLines = ReadRemainingLines(cursor);

        // Assert
        skipped.Should().BeTrue();
        remainingLines.Should().Equal(lines.Skip(1));
    }

    [Fact]
    public void TrySkipLine_WhenLastLineWithoutNewlineIsLongerThanOneChunk_ReturnsFalse()
    {
        // Arrange
        using var mmap = OpenMmap($"{{\"short\":1}}\n{{\"long\":\"{new string('c', 250_000)}\"}}");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var firstSkipped = cursor.TrySkipLine();
        var secondSkipped = cursor.TrySkipLine();

        // Assert
        firstSkipped.Should().BeTrue();
        secondSkipped.Should().BeFalse();
    }

    [Fact]
    public void TrySkipLine_WithCrLfSplitAcrossChunks_NextReadReturnsFollowingLine()
    {
        // Arrange
        var lines = BuildLinesWithCrLfSplitAcrossChunks();
        using var mmap = OpenMmap(string.Join("\r\n", lines) + "\r\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var skipped = cursor.TrySkipLine();
        var remainingLines = ReadRemainingLines(cursor);

        // Assert
        skipped.Should().BeTrue();
        remainingLines.Should().Equal(lines.Skip(1));
    }

    [Fact]
    public void TrySkipLine_WithNonZeroStartOffset_SkipsLineAtThatOffset()
    {
        // Arrange
        using var mmap = OpenMmap("{\"a\":1}\n{\"b\":2}\n{\"c\":3}\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 8);

        // Act
        var skipped = cursor.TrySkipLine();
        var remainingLines = ReadRemainingLines(cursor);

        // Assert
        skipped.Should().BeTrue();
        remainingLines.Should().Equal("{\"c\":3}");
    }

    // TryReadLine

    [Fact]
    public void TryReadLine_WithShortLinesSpanningMultipleChunks_ReturnsEveryLineIntact()
    {
        // Arrange
        var expectedLines = BuildManyShortLines();
        using var mmap = OpenMmap(string.Join('\n', expectedLines) + "\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var lines = ReadRemainingLines(cursor);

        // Assert
        lines.Should().Equal(expectedLines);
    }

    // A 3-byte character starting at 65,534 / 65,535 is split across the 64 KB chunk boundary; at 65,536 it is not
    [Theory]
    [InlineData(65_533)]
    [InlineData(65_534)]
    [InlineData(65_535)]
    [InlineData(65_536)]
    public void TryReadLine_WithMultiByteCharacterAtChunkBoundary_ReturnsEveryLineIntact(int asciiPrefixLength)
    {
        // Arrange
        string[] expectedLines =
        [
            new string('a', asciiPrefixLength) + "あいう",
            "日本語のテキスト",
            "{\"k\":\"値\"}",
        ];
        using var mmap = OpenMmap(string.Join('\n', expectedLines) + "\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var lines = ReadRemainingLines(cursor);

        // Assert
        lines.Should().Equal(expectedLines);
    }

    [Fact]
    public void TryReadLine_WithManyJapaneseLinesSpanningMultipleChunks_ReturnsEveryLineIntact()
    {
        // Arrange
        string[] expectedLines = [.. Enumerable.Range(0, 5_000).Select(i => $"{{\"名前\":\"テスト{i}\"}}")];
        using var mmap = OpenMmap(string.Join('\n', expectedLines) + "\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var lines = ReadRemainingLines(cursor);

        // Assert
        lines.Should().Equal(expectedLines);
    }

    [Fact]
    public void TryReadLine_AtEndOfFile_ReturnsFalse()
    {
        // Arrange
        const string content = "{\"a\":1}\n";
        using var mmap = OpenMmap(content);
        using var cursor = new LineChunkCursor(mmap, startOffset: content.Length);

        // Act
        var hasLine = cursor.TryReadLine(out var line);

        // Assert
        hasLine.Should().BeFalse();
        line.Length.Should().Be(0);
    }

    [Fact]
    public void TryReadLine_AfterLastTerminatedLine_ReturnsFalse()
    {
        // Arrange
        using var mmap = OpenMmap("{\"a\":1}\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var hasFirstLine = cursor.TryReadLine(out var firstLine);
        var hasSecondLine = cursor.TryReadLine(out _);

        // Assert
        hasFirstLine.Should().BeTrue();
        Encoding.UTF8.GetString(firstLine.Span).Should().Be("{\"a\":1}");
        hasSecondLine.Should().BeFalse();
    }

    [Fact]
    public void TryReadLine_WithEmptyLines_ReturnsEmptyLinesBetweenOtherLines()
    {
        // Arrange
        using var mmap = OpenMmap("\n{\"a\":1}\n\n{\"b\":2}\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var lines = ReadRemainingLines(cursor);

        // Assert
        lines.Should().Equal(string.Empty, "{\"a\":1}", string.Empty, "{\"b\":2}");
    }

    [Fact]
    public void TryReadLine_WhenLastLineHasNoNewline_ReturnsLastLine()
    {
        // Arrange
        using var mmap = OpenMmap("{\"a\":1}\n{\"b\":");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var lines = ReadRemainingLines(cursor);

        // Assert
        lines.Should().Equal("{\"a\":1}", "{\"b\":");
    }

    [Fact]
    public void TryReadLine_WithLinesLongerThanOneChunk_ReturnsEveryLineIntact()
    {
        // Arrange
        var expectedLines = BuildLongAndShortLines();
        using var mmap = OpenMmap(string.Join('\n', expectedLines) + "\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var lines = ReadRemainingLines(cursor);

        // Assert
        lines.Should().Equal(expectedLines);
    }

    [Fact]
    public void TryReadLine_WhenLastLineWithoutNewlineIsLongerThanOneChunk_ReturnsLastLineIntact()
    {
        // Arrange
        string[] expectedLines = ["{\"short\":1}", $"{{\"long\":\"{new string('c', 250_000)}\"}}"];
        using var mmap = OpenMmap(string.Join('\n', expectedLines));
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var lines = ReadRemainingLines(cursor);

        // Assert
        lines.Should().Equal(expectedLines);
    }

    [Theory]
    [InlineData(65_534)]
    [InlineData(65_535)]
    [InlineData(65_536)]
    [InlineData(65_537)]
    [InlineData(65_538)]
    public void TryReadLine_WithCrLfLineEndingsNearChunkSizeLengths_TrimsLineEndingsFromEveryLine(int firstLineLength)
    {
        // Arrange
        string[] expectedLines = [new string('a', firstLineLength), new string('b', 100), new string('c', 100)];
        using var mmap = OpenMmap(string.Join("\r\n", expectedLines) + "\r\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var lines = ReadRemainingLines(cursor);

        // Assert
        lines.Should().Equal(expectedLines);
    }

    [Fact]
    public void TryReadLine_WithCrLfSplitAcrossChunks_TrimsLineEndingsFromEveryLine()
    {
        // Arrange
        var expectedLines = BuildLinesWithCrLfSplitAcrossChunks();
        using var mmap = OpenMmap(string.Join("\r\n", expectedLines) + "\r\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 0);

        // Act
        var lines = ReadRemainingLines(cursor);

        // Assert
        lines.Should().Equal(expectedLines);
    }

    [Fact]
    public void TryReadLine_WithNonZeroStartOffset_ReturnsLinesFromThatOffset()
    {
        // Arrange
        using var mmap = OpenMmap("{\"a\":1}\n{\"b\":2}\n{\"c\":3}\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: 8);

        // Act
        var lines = ReadRemainingLines(cursor);

        // Assert
        lines.Should().Equal("{\"b\":2}", "{\"c\":3}");
    }

    [Fact]
    public void TryReadLine_WithNonZeroStartOffsetAtLineLongerThanOneChunk_ReturnsRemainingLinesIntact()
    {
        // Arrange
        var lines = BuildLongAndShortLines();
        var offsetOfThirdLine = lines[0].Length + 1 + lines[1].Length + 1;
        using var mmap = OpenMmap(string.Join('\n', lines) + "\n");
        using var cursor = new LineChunkCursor(mmap, startOffset: offsetOfThirdLine);

        // Act
        var readLines = ReadRemainingLines(cursor);

        // Assert
        readLines.Should().Equal(lines.Skip(2));
    }
}

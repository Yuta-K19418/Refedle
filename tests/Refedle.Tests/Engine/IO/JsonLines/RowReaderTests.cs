using System.Text;
using AwesomeAssertions;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed partial class RowReaderTests : IDisposable
{
    private readonly string _testFilePath;
    private bool _disposed;

    public RowReaderTests()
    {
        _testFilePath = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            File.Delete(_testFilePath);
            _disposed = true;
        }
    }

    private void WriteTestContent(string content, Encoding? encoding = null)
    {
        File.WriteAllText(_testFilePath, content, encoding ?? new UTF8Encoding(false));
    }

    [Fact]
    public void Constructor_WithNullFilePath_ThrowsArgumentNullException()
    {
        // Arrange
        string? filePath = null;

        // Act
        var act = () => new RowReader(filePath!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ReadLines_WithSingleJsonLine_ReturnsOneLine()
    {
        // Arrange
        WriteTestContent("{\"id\":1}\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 1);

        // Assert
        lines.Should().ContainSingle();
        var lineString = Encoding.UTF8.GetString(lines[0].Span);
        lineString.Should().Be("{\"id\":1}");
    }

    [Fact]
    public void ReadLines_WithMultipleJsonLines_ReturnsAllLines()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n{\"b\":2}\n{\"c\":3}\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 3);

        // Assert
        lines.Should().HaveCount(3);
        Encoding.UTF8.GetString(lines[0].Span).Should().Be("{\"a\":1}");
        Encoding.UTF8.GetString(lines[1].Span).Should().Be("{\"b\":2}");
        Encoding.UTF8.GetString(lines[2].Span).Should().Be("{\"c\":3}");
    }

    [Fact]
    public void ReadLines_WithLinesToSkip_SkipsCorrectly()
    {
        // Arrange
        WriteTestContent("{\"skip\":0}\n{\"read\":1}\n{\"read\":2}\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 1, linesToRead: 2);

        // Assert
        lines.Should().HaveCount(2);
        Encoding.UTF8.GetString(lines[0].Span).Should().Be("{\"read\":1}");
        Encoding.UTF8.GetString(lines[1].Span).Should().Be("{\"read\":2}");
    }

    [Fact]
    public void ReadLines_WithLinesToRead_LimitsCorrectly()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n{\"b\":2}\n{\"c\":3}\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 2);

        // Assert
        lines.Should().HaveCount(2);
        Encoding.UTF8.GetString(lines[0].Span).Should().Be("{\"a\":1}");
        Encoding.UTF8.GetString(lines[1].Span).Should().Be("{\"b\":2}");
    }

    [Fact]
    public void Constructor_WithEmptyFile_ThrowsInvalidOperationException()
    {
        // Arrange
        WriteTestContent("");

        // Act
        var act = () => new RowReader(_testFilePath);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReadLines_WithInvalidJsonLine_ReturnsBytesAsIs()
    {
        // Arrange
        WriteTestContent("invalid json\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 1);

        // Assert
        // Without validation, raw bytes are returned as-is
        lines.Should().ContainSingle();
        var lineString = Encoding.UTF8.GetString(lines[0].Span);
        lineString.Should().Be("invalid json");
    }

    [Fact]
    public void ReadLines_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        WriteTestContent("{\"test\":1}\n");
        var reader = new RowReader(_testFilePath);
        reader.Dispose();

        // Act
        var act = () => reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 1);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ReadLines_WithOffsetBeyondEOF_ReturnsEmptyList()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n");
        using var reader = new RowReader(_testFilePath);
        long largeOffset = 1000; // beyond file size

        // Act
        var lines = reader.ReadLines(byteOffset: largeOffset, linesToSkip: 0, linesToRead: 1);

        // Assert
        lines.Should().BeEmpty();
    }

    [Fact]
    public void ReadLines_WithIncompleteLineAtEOF_ReturnsBytesAsIs()
    {
        // Arrange
        // Incomplete JSON line without newline at EOF
        WriteTestContent("{\"incomplete\":");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 1);

        // Assert
        // Incomplete lines at EOF return raw bytes as-is
        lines.Should().ContainSingle();
        var lineString = Encoding.UTF8.GetString(lines[0].Span);
        lineString.Should().Be("{\"incomplete\":");
    }

    [Fact]
    public void ReadLines_WithLinesToSkipExceedingTotalLines_ReturnsEmptyList()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n{\"b\":2}\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 10, linesToRead: 1);

        // Assert
        lines.Should().BeEmpty();
    }

    [Fact]
    public void ReadLines_WithCrLfLineEndings_TrimsCorrectly()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\r\n{\"b\":2}\r\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 2);

        // Assert
        lines.Should().HaveCount(2);
        Encoding.UTF8.GetString(lines[0].Span).Should().Be("{\"a\":1}");
        Encoding.UTF8.GetString(lines[1].Span).Should().Be("{\"b\":2}");
    }

    [Fact]
    public void ReadLines_WithZeroLinesToRead_ReturnsEmptyList()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n{\"b\":2}\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 0);

        // Assert
        lines.Should().BeEmpty();
    }

    [Fact]
    public void ReadLines_WithValidLineWithoutNewlineAtEOF_ReturnsLine()
    {
        // Arrange
        // Valid JSON line without newline at EOF
        WriteTestContent("{\"valid\":true}");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 1);

        // Assert
        lines.Should().ContainSingle();
        var lineString = Encoding.UTF8.GetString(lines[0].Span);
        lineString.Should().Be("{\"valid\":true}");
    }

    [Fact]
    public void ReadLines_WhenFileHasBOM_ReturnsCorrectLines()
    {
        // Arrange
        // Write JSONL with BOM
        var content = "{\"id\":1,\"name\":\"Alice\"}\n";
        WriteTestContent(content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        // Act
        using var reader = new RowReader(_testFilePath);
        var lines = reader.ReadLines(0, 0, 10);

        // Assert
        lines.Should().ContainSingle();
        var lineContent = Encoding.UTF8.GetString(lines[0].Span);
        lineContent.Should().Be("{\"id\":1,\"name\":\"Alice\"}");
    }

    // Skip loop tests (data <= 1 MB)

    [Fact]
    public void ReadLines_SkipLoop_SmallData_EscapedNewlineInValue_ReadsNextLineCorrectly()
    {
        // Arrange
        // First line has escaped \n in value, second line should be read correctly
        var content = "{\"text\":\"line1\\nline2\",\"id\":1}\n{\"text\":\"next line\",\"id\":2}\n";
        WriteTestContent(content);
        using var reader = new RowReader(_testFilePath);

        // Act
        // Skip first line, read second line
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 1, linesToRead: 1);

        // Assert
        lines.Should().ContainSingle();
        var lineString = Encoding.UTF8.GetString(lines[0].Span);
        lineString.Should().Be("{\"text\":\"next line\",\"id\":2}");
    }

    [Fact]
    public void ReadLines_SkipLoop_SmallData_UnclosedQuoteWithEmbeddedNewline_ReturnsAdjacentLine()
    {
        // Arrange
        // First line has unclosed quote with literal newline — without validation,
        // the newline splits the content into two "lines" at the byte level.
        // The skip loop skips the first byte-level line, so we read the second.
        var content = "{\"text\":\"line1\n{\"text\":\"valid line\",\"id\":2}\n";
        WriteTestContent(content);
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 1, linesToRead: 1);

        // Assert
        lines.Should().ContainSingle();
        var lineString = Encoding.UTF8.GetString(lines[0].Span);
        lineString.Should().Be("{\"text\":\"valid line\",\"id\":2}");
    }

    [Fact]
    public void ReadLines_SkipLoop_SmallData_NormalLineEnding_ReadsNextLineCorrectly()
    {
        // Arrange
        var content = "{\"id\":1}\n{\"id\":2}\n{\"id\":3}\n";
        WriteTestContent(content);
        using var reader = new RowReader(_testFilePath);

        // Act
        // Skip first line, read next two lines
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 1, linesToRead: 2);

        // Assert
        lines.Should().HaveCount(2);
        Encoding.UTF8.GetString(lines[0].Span).Should().Be("{\"id\":2}");
        Encoding.UTF8.GetString(lines[1].Span).Should().Be("{\"id\":3}");
    }

    // Skip loop tests (data > 1 MB)

    [Fact]
    [Trait("Category", "LargeData")]
    public void ReadLines_SkipLoop_LargeData_EscapedNewlineInValue_ReadsNextLineCorrectly()
    {
        // Arrange
        // Line 0: valid >1MB line with escaped \n in value — single line exceeds maxSearch, triggering the multi-call path in skip loop
        var largeEscapedValue = new string('a', 1_090_000) + "\\n" + new string('b', 5_000);
        var content = $"{{\"text\":\"{largeEscapedValue}\",\"id\":0}}\n{{\"text\":\"next line\",\"id\":1}}\n";
        WriteTestContent(content);
        using var reader = new RowReader(_testFilePath);

        // Act
        // Skip large line (>1MB), read next small line
        var linesRead = reader.ReadLines(byteOffset: 0, linesToSkip: 1, linesToRead: 1);

        // Assert
        linesRead.Should().ContainSingle();
        Encoding.UTF8.GetString(linesRead[0].Span).Should().Be("{\"text\":\"next line\",\"id\":1}");
    }

    [Fact]
    [Trait("Category", "LargeData")]
    public void ReadLines_SkipLoop_LargeData_UnclosedQuoteWithEmbeddedNewline_ReturnsAdjacentLine()
    {
        // Arrange
        // Malformed: unclosed quote, value > 1MB so FindNextLineLength is called twice.
        // Without validation, the embedded newline splits the content at the byte level.
        var malformedValue = new string('a', 1_100_000);
        var content = $"{{\"text\":\"{malformedValue}\n{{\"id\":0}}\n";
        WriteTestContent(content);
        using var reader = new RowReader(_testFilePath);

        // Act
        // Skip the first byte-level line (up to embedded newline), read next
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 1, linesToRead: 1);

        // Assert
        lines.Should().ContainSingle();
        var lineString = Encoding.UTF8.GetString(lines[0].Span);
        lineString.Should().Be("{\"id\":0}");
    }

    [Fact]
    [Trait("Category", "LargeData")]
    public void ReadLines_SkipLoop_LargeData_NormalLineEnding_ReadsNextLineCorrectly()
    {
        // Arrange
        // Line 0: valid >1MB line — single line exceeds maxSearch, triggering the multi-call path in skip loop
        var largeValue = new string('a', 1_100_000);
        var content = $"{{\"data\":\"{largeValue}\"}}\n{{\"id\":1}}\n{{\"id\":2}}\n";
        WriteTestContent(content);
        using var reader = new RowReader(_testFilePath);

        // Act
        // Skip large line (>1MB), read next two small lines
        var linesRead = reader.ReadLines(byteOffset: 0, linesToSkip: 1, linesToRead: 2);

        // Assert
        linesRead.Should().HaveCount(2);
        Encoding.UTF8.GetString(linesRead[0].Span).Should().Be("{\"id\":1}");
        Encoding.UTF8.GetString(linesRead[1].Span).Should().Be("{\"id\":2}");
    }

    // Read loop tests (data <= 1 MB)

    [Fact]
    public void ReadLines_ReadLoop_SmallData_EscapedNewlineInValue_ReturnsCorrectBytes()
    {
        // Arrange
        var content = "{\"text\":\"line1\\nline2\",\"id\":1}\n{\"text\":\"next\",\"id\":2}\n";
        WriteTestContent(content);
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 2);

        // Assert
        lines.Should().HaveCount(2);
        Encoding.UTF8.GetString(lines[0].Span).Should().Be("{\"text\":\"line1\\nline2\",\"id\":1}");
        Encoding.UTF8.GetString(lines[1].Span).Should().Be("{\"text\":\"next\",\"id\":2}");
    }

    [Fact]
    public void ReadLines_ReadLoop_SmallData_UnclosedQuoteWithEmbeddedNewline_ReturnsBothLinesAsIs()
    {
        // Arrange
        // Malformed: unclosed quote with literal newline — without validation,
        // both byte-level lines are returned as-is.
        var content = "{\"text\":\"malformed\n{\"text\":\"valid\",\"id\":2}\n";
        WriteTestContent(content);
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 2);

        // Assert
        lines.Should().HaveCount(2);
        Encoding.UTF8.GetString(lines[0].Span).Should().Be("{\"text\":\"malformed");
        Encoding.UTF8.GetString(lines[1].Span).Should().Be("{\"text\":\"valid\",\"id\":2}");
    }

    [Fact]
    public void ReadLines_ReadLoop_SmallData_NormalLineEnding_ReturnsCorrectBytes()
    {
        // Arrange
        var content = "{\"id\":1}\n{\"id\":2}\n{\"id\":3}\n";
        WriteTestContent(content);
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 3);

        // Assert
        lines.Should().HaveCount(3);
        Encoding.UTF8.GetString(lines[0].Span).Should().Be("{\"id\":1}");
        Encoding.UTF8.GetString(lines[1].Span).Should().Be("{\"id\":2}");
        Encoding.UTF8.GetString(lines[2].Span).Should().Be("{\"id\":3}");
    }

    // Read loop tests (data > 1 MB)

    [Fact]
    [Trait("Category", "LargeData")]
    public void ReadLines_ReadLoop_LargeData_EscapedNewlineInValue_ReturnsCorrectBytes()
    {
        // Arrange
        // Line 0: valid >1MB line with escaped \n in value — single line exceeds maxSearch, triggering the multi-call path in read loop
        var largeEscapedValue = new string('a', 1_090_000) + "\\n" + new string('b', 5_000);
        var largeLine = $"{{\"text\":\"{largeEscapedValue}\",\"id\":0}}";
        var content = largeLine + "\n" + "{\"text\":\"next\",\"id\":1}\n";
        WriteTestContent(content);
        using var reader = new RowReader(_testFilePath);

        // Act
        var linesRead = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 2);

        // Assert
        linesRead.Should().HaveCount(2);
        Encoding.UTF8.GetString(linesRead[0].Span).Should().Be(largeLine);
        Encoding.UTF8.GetString(linesRead[1].Span).Should().Be("{\"text\":\"next\",\"id\":1}");
    }

    [Fact]
    [Trait("Category", "LargeData")]
    public void ReadLines_ReadLoop_LargeData_UnclosedQuoteWithEmbeddedNewline_ReturnsBothLinesAsIs()
    {
        // Arrange
        // Malformed: unclosed quote, value > 1MB so FindNextLineLength is called twice.
        // Without validation, both byte-level lines are returned as-is.
        var malformedValue = new string('a', 1_100_000);
        var content = $"{{\"text\":\"{malformedValue}\n{{\"id\":0}}\n";
        WriteTestContent(content);
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 2);

        // Assert
        lines.Should().HaveCount(2);
        var firstLine = Encoding.UTF8.GetString(lines[0].Span);
        firstLine.Should().Be($"{{\"text\":\"{malformedValue}");
        Encoding.UTF8.GetString(lines[1].Span).Should().Be("{\"id\":0}");
    }

    [Fact]
    [Trait("Category", "LargeData")]
    public void ReadLines_ReadLoop_LargeData_NormalLineEnding_ReturnsCorrectBytes()
    {
        // Arrange
        // Line 0: valid >1MB line — single line exceeds maxSearch, triggering the multi-call path in read loop
        var largeValue = new string('a', 1_100_000);
        var largeLine = $"{{\"data\":\"{largeValue}\"}}";
        var content = largeLine + "\n" + "{\"id\":1}\n";
        WriteTestContent(content);
        using var reader = new RowReader(_testFilePath);

        // Act
        var linesRead = reader.ReadLines(byteOffset: 0, linesToSkip: 0, linesToRead: 2);

        // Assert
        linesRead.Should().HaveCount(2);
        Encoding.UTF8.GetString(linesRead[0].Span).Should().Be(largeLine);
        Encoding.UTF8.GetString(linesRead[1].Span).Should().Be("{\"id\":1}");
    }

    [Fact]
    public void ReadLines_WithNonZeroByteOffset_ReturnsCorrectLines()
    {
        // Arrange
        var content = "{\"id\":0}\n{\"id\":1}\n{\"id\":2}\n{\"id\":3}\n{\"id\":4}\n";
        WriteTestContent(content);
        using var reader = new RowReader(_testFilePath);

        // Calculate byte offset to the middle of the file (after first 2 lines)
        var byteOffset = Encoding.UTF8.GetBytes("{\"id\":0}\n{\"id\":1}\n").Length;

        // Act
        var lines = reader.ReadLines(byteOffset: byteOffset, linesToSkip: 0, linesToRead: 2);

        // Assert
        lines.Should().HaveCount(2);
        Encoding.UTF8.GetString(lines[0].Span).Should().Be("{\"id\":2}");
        Encoding.UTF8.GetString(lines[1].Span).Should().Be("{\"id\":3}");
    }
}

using System.Text;
using AwesomeAssertions;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.Tests.Engine.IO.DrillDown;

/// <summary>
/// Tests for <see cref="FileChunkReader"/>'s buffer-refill path, CRLF trimming,
/// and range reading. Each test constructs a real <see cref="MmapService"/> over a temp file so the
/// <see cref="FileChunkReader"/> helpers are exercised directly, not through FullAggregationScanner.
/// The boundary carry-over scenario uses the real <see cref="FileChunkReader.BufferSize"/> (1 MiB)
/// with a file large enough to straddle it, mirroring FullAggregationScannerTests' boundary fixture.
/// </summary>
public sealed class FileChunkReaderTests : IDisposable
{
    private readonly string _testFilePath;

    public FileChunkReaderTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"filechunkreader_{Guid.NewGuid()}.txt");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void FillBuffer_WhenLineSpansBufferBoundary_PreservesCarriedOverBytes()
    {
        // Arrange — file larger than BufferSize so a follow-up FillBuffer has data to top up with,
        // mirroring FullAggregationScannerTests' 1 MiB boundary fixture.
        const int carryOverLen = 100;
        var padding = new string('a', FileChunkReader.BufferSize);
        var tail = new string('b', 500);
        File.WriteAllText(_testFilePath, padding + tail);
        using var mmap = MmapService.Open(_testFilePath).Value;
        var buffer = new byte[FileChunkReader.BufferSize];
        // The first carryOverLen bytes are the leftover of a line that spans the 1 MiB boundary;
        // mark them so we can prove FillBuffer preserves them in place.
        buffer.AsSpan(0, carryOverLen).Fill((byte)'X');
        var expectedCarryOver = new byte[carryOverLen];
        Array.Fill(expectedCarryOver, (byte)'X');

        // Act — resume at the 1 MiB boundary, carrying carryOverLen bytes forward.
        var (dataEnd, fileOffset) = FileChunkReader.FillBuffer(
            mmap, buffer, carryOverLen, FileChunkReader.BufferSize, "test");

        // Assert — the carry-over bytes are intact and the tail is appended right after them.
        dataEnd.Should().Be(carryOverLen + tail.Length);
        fileOffset.Should().Be(FileChunkReader.BufferSize + tail.Length);
        buffer.AsSpan(0, carryOverLen).ToArray().Should().Equal(expectedCarryOver);
        Encoding.ASCII.GetString(buffer, carryOverLen, tail.Length).Should().Be(tail);
    }

    [Fact]
    public void FillBuffer_WhenRemainingLengthEqualsBufferSize_ThrowsNotSupportedException()
    {
        // Arrange — the guard throws before any mmap/buffer read, so only the buffer capacity matters.
        File.WriteAllText(_testFilePath, "0123456789");
        using var mmap = MmapService.Open(_testFilePath).Value;
        var buffer = new byte[FileChunkReader.BufferSize];

        // Act
        var act = () => FileChunkReader.FillBuffer(mmap, buffer, FileChunkReader.BufferSize, 0, "test line");

        // Assert
        act.Should().Throw<NotSupportedException>().WithMessage("test line exceeds maximum supported size.");
    }

    [Fact]
    public void TrimTrailingCr_WithTrailingCarriageReturn_RemovesLastByte()
    {
        // Arrange
        var line = "abc\r"u8;

        // Act
        var result = FileChunkReader.TrimTrailingCr(line);

        // Assert
        result.ToArray().Should().Equal("abc"u8.ToArray());
    }

    [Fact]
    public void TrimTrailingCr_WithoutTrailingCarriageReturn_ReturnsInputUnchanged()
    {
        // Arrange
        var line = "abc"u8;

        // Act
        var result = FileChunkReader.TrimTrailingCr(line);

        // Assert
        result.ToArray().Should().Equal("abc"u8.ToArray());
    }

    public static IEnumerable<object[]> ReadFileRangeCases()
    {
        // Range [2, 5) of "0123456789" → "234".
        yield return [Encoding.ASCII.GetBytes("0123456789"), 2L, 5L, Encoding.ASCII.GetBytes("234")];
        // Range to EOF [7, 10) of a 10-byte file → "789" (endOffset == file length).
        yield return [Encoding.ASCII.GetBytes("0123456789"), 7L, 10L, Encoding.ASCII.GetBytes("789")];
        // Empty range [3, 3) → zero-length result.
        yield return [Encoding.ASCII.GetBytes("0123456789"), 3L, 3L, Array.Empty<byte>()];
    }

    [Theory]
    [MemberData(nameof(ReadFileRangeCases))]
    public void ReadFileRange_ForGivenRange_ReturnsExactBytes(byte[] content, long start, long end, byte[] expected)
    {
        // Arrange
        File.WriteAllBytes(_testFilePath, content);
        using var mmap = MmapService.Open(_testFilePath).Value;

        // Act
        var result = FileChunkReader.ReadFileRange(mmap, start, end);

        // Assert
        result.ToArray().Should().Equal(expected);
    }
}

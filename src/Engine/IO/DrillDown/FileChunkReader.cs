namespace Refedle.Engine.IO.DrillDown;

/// <summary>
/// Stateless buffer-management helpers for chunked forward scans over a memory-mapped file.
/// Shared implementation detail of <see cref="FullAggregationScanner"/>, split out to keep both
/// classes under the project's per-class line limit.
/// </summary>
internal static class FileChunkReader
{
    internal const int BufferSize = 1024 * 1024;

    /// <summary>
    /// Trims a single trailing CR byte from <paramref name="line"/>, if present, so CRLF and LF
    /// line endings produce identical line contents.
    /// </summary>
    internal static ReadOnlySpan<byte> TrimTrailingCr(ReadOnlySpan<byte> line) =>
        line.Length > 0 && line[^1] == (byte)'\r' ? line[..^1] : line;

    /// <summary>
    /// Reads the byte range <c>[startOffset, endOffset)</c> from <paramref name="mmap"/> into a
    /// freshly-allocated buffer.
    /// </summary>
    internal static JsonRawBytes ReadFileRange(MmapService mmap, long startOffset, long endOffset)
    {
        var length = (int)(endOffset - startOffset);
        var bytes = new byte[length];
        mmap.Read(startOffset, bytes);
        return bytes;
    }

    /// <summary>
    /// Tops up <paramref name="buffer"/> with the next chunk of <paramref name="mmap"/> starting at
    /// <paramref name="fileOffset"/>, preserving the first <paramref name="remainingLen"/> bytes
    /// already in the buffer.
    /// </summary>
    /// <param name="mmap">The memory-mapped file to read from.</param>
    /// <param name="buffer">The buffer to top up in place.</param>
    /// <param name="remainingLen">The count of unread bytes already present at the start of <paramref name="buffer"/>.</param>
    /// <param name="fileOffset">The file offset to resume reading from.</param>
    /// <param name="sizeContext">Description used in the overflow exception message.</param>
    /// <returns>The new data length in the buffer, and the advanced file offset.</returns>
    internal static (int dataEnd, long fileOffset) FillBuffer(
        MmapService mmap, byte[] buffer, int remainingLen, long fileOffset, string sizeContext)
    {
        if (remainingLen == BufferSize)
        {
            throw new NotSupportedException($"{sizeContext} exceeds maximum supported size.");
        }

        var available = mmap.Length - fileOffset;
        var toRead = (int)Math.Min(BufferSize - remainingLen, Math.Max(0L, available));
        if (toRead > 0)
        {
            mmap.Read(fileOffset, buffer.AsSpan(remainingLen, toRead));
        }

        return (remainingLen + toRead, fileOffset + toRead);
    }
}

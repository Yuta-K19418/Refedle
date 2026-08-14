using System.Buffers;

namespace Refedle.Engine.IO.JsonLines;

/// <summary>
/// Reads JSON line bytes from a file using indexed byte offsets.
/// Returns raw JSON bytes per line (not TreeNode) to maintain Engine/App layer separation.
/// </summary>
public sealed class RowReader : IDisposable
{
    private readonly MmapService _mmap;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RowReader"/> class.
    /// </summary>
    /// <param name="filePath">Path to the JSON Lines file.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null.</exception>
    public RowReader(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var mmapResult = MmapService.Open(filePath);
        if (!mmapResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Failed to open memory-mapped file: {mmapResult.Error}"
            );
        }

        _mmap = mmapResult.Value;
    }

    /// <summary>
    /// Reads raw JSON line bytes for a specified range.
    /// </summary>
    /// <param name="byteOffset">Starting byte offset in the file.</param>
    /// <param name="linesToSkip">Number of lines to skip from the offset.</param>
    /// <param name="linesToRead">Maximum number of lines to read.</param>
    /// <returns>A list of raw JSON line bytes.</returns>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    /// <exception cref="NotSupportedException">The JSON line exceeds the supported size limit.</exception>
    public IReadOnlyList<JsonRawBytes> ReadLines(
        long byteOffset,
        int linesToSkip,
        int linesToRead
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        List<JsonRawBytes> result = [];
        result.EnsureCapacity(linesToRead);
        var currentOffset = byteOffset;

        // Skip UTF-8 BOM if present at the beginning of the file
        if (currentOffset == 0 && _mmap.Length >= 3)
        {
            Span<byte> bomHeader = stackalloc byte[3];
            _mmap.Read(0, bomHeader);
            if (HasUtf8Bom(bomHeader))
            {
                currentOffset = 3;
            }
        }

        var skipResult = SkipLines(currentOffset, linesToSkip);
        if (!skipResult.reachedTarget)
        {
            // No more data to skip - when trying to skip beyond EOF, there are no lines to read
            return result;
        }

        ReadRequestedLines(skipResult.offset, linesToRead, result);
        return result;
    }

    private (bool reachedTarget, long offset) SkipLines(long startOffset, int linesToSkip)
    {
        var skipped = 0;
        var skipLineStartOffset = startOffset;
        var skipIncompleteBytes = 0L;

        while (skipped < linesToSkip)
        {
            var (lineCompleted, bytesConsumed) = FindNextLineLength(skipLineStartOffset + skipIncompleteBytes);
            // bytesConsumed <= 0 indicates EOF or error
            if (bytesConsumed <= 0)
            {
                return (false, skipLineStartOffset);
            }

            if (!lineCompleted)
            {
                skipIncompleteBytes += bytesConsumed;
                continue;
            }

            var totalLineBytes = bytesConsumed + skipIncompleteBytes;

            // Lines exceeding ~2 GB are not currently supported; revisit if demand arises.
            if (totalLineBytes > Array.MaxLength)
            {
                throw new NotSupportedException("JSON line exceeds maximum supported size.");
            }

            skipLineStartOffset += totalLineBytes;
            skipIncompleteBytes = 0;
            skipped++;
        }

        return (true, skipLineStartOffset);
    }

    private void ReadRequestedLines(long startOffset, int linesToRead, List<JsonRawBytes> result)
    {
        var currentOffset = startOffset;
        var linesRead = 0;
        var incompleteLineBytes = 0L;

        while (linesRead < linesToRead)
        {
            var (lineCompleted, bytesConsumed) = FindNextLineLength(currentOffset + incompleteLineBytes);
            if (bytesConsumed <= 0)
            {
                HandleIncompleteLineAtEof(currentOffset, incompleteLineBytes, result);
                return;
            }

            if (!lineCompleted)
            {
                incompleteLineBytes += bytesConsumed;
                continue;
            }

            var totalLineBytes = bytesConsumed + incompleteLineBytes;

            // Lines exceeding ~2 GB are not currently supported; revisit if demand arises.
            if (totalLineBytes > Array.MaxLength)
            {
                throw new NotSupportedException("JSON line exceeds maximum supported size.");
            }

            var lineBuffer = ArrayPool<byte>.Shared.Rent((int)totalLineBytes);
            try
            {
                var lineSpan = lineBuffer.AsSpan(0, (int)totalLineBytes);
                _mmap.Read(currentOffset, lineSpan);
                var trimmedSpan = TrimNewline(lineSpan);

                var lineBytes = new byte[trimmedSpan.Length];
                trimmedSpan.CopyTo(lineBytes);
                result.Add(lineBytes.AsMemory());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(lineBuffer);
            }

            currentOffset += totalLineBytes;
            incompleteLineBytes = 0;
            linesRead++;
        }
    }

    private static bool HasUtf8Bom(ReadOnlySpan<byte> header)
    {
        return header.Length >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF;
    }

    private static ReadOnlySpan<byte> TrimNewline(ReadOnlySpan<byte> span)
    {
        // Remove trailing \r\n or \n
        if (span.Length > 0 && span[span.Length - 1] == '\n')
        {
            span = span[..^1];
            if (span.Length > 0 && span[span.Length - 1] == '\r')
            {
                span = span[..^1];
            }
        }

        return span;
    }

    private void HandleIncompleteLineAtEof(long offset, long incompleteLineBytes, List<JsonRawBytes> result)
    {
        if (incompleteLineBytes <= 0)
        {
            return;
        }

        // Lines exceeding ~2 GB are not currently supported; revisit if demand arises.
        if (incompleteLineBytes > Array.MaxLength)
        {
            throw new NotSupportedException("JSON line exceeds maximum supported size.");
        }

        // This is the last line without a newline
        var lastLineBytes = new byte[(int)incompleteLineBytes];
        _mmap.Read(offset, lastLineBytes);
        var lastTrimmedSpan = TrimNewline(lastLineBytes.AsSpan());
        if (lastTrimmedSpan.Length <= 0)
        {
            return;
        }

        result.Add(lastLineBytes.AsMemory(0, lastTrimmedSpan.Length));
    }

    private (bool lineCompleted, int bytesConsumed) FindNextLineLength(long startOffset)
    {
        const int initialSize = 4096;
        const int maxSearch = 1024 * 1024;
        var remaining = _mmap.Length - startOffset;
        if (remaining <= 0)
        {
            return (false, 0);
        }

        var firstRead = (int)Math.Min(initialSize, remaining);
        var buffer = ArrayPool<byte>.Shared.Rent(firstRead);
        try
        {
            var span = buffer.AsSpan(0, firstRead);
            _mmap.Read(startOffset, span);

            var index = span.IndexOf((byte)'\n');
            if (index != -1)
            {
                return (true, index + 1);
            }

            if (remaining == firstRead)
            {
                return (false, firstRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        // Line exceeds 4KB — fall back to 1MB search
        var searchLength = (int)Math.Min(maxSearch, remaining);
        var bigBuffer = ArrayPool<byte>.Shared.Rent(searchLength);
        try
        {
            var span = bigBuffer.AsSpan(0, searchLength);
            _mmap.Read(startOffset, span);

            var index = span.IndexOf((byte)'\n');
            return index == -1 ? (false, searchLength) : (true, index + 1);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bigBuffer);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _mmap?.Dispose();
            _disposed = true;
        }
    }
}

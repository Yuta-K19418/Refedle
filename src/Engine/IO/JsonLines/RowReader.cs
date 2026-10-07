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
        var currentOffset = SkipBomIfAtStart(byteOffset);

        using var cursor = new LineChunkCursor(_mmap, currentOffset);
        if (!SkipLines(cursor, linesToSkip))
        {
            // No more data to skip - when trying to skip beyond EOF, there are no lines to read
            return result;
        }

        ReadRequestedLines(cursor, linesToRead, result);
        return result;
    }

    /// <summary>
    /// Lazily enumerates raw JSON line bytes from the start of the file in a single forward pass.
    /// </summary>
    /// <param name="linesToSkip">Number of lines to skip from the start of the file.</param>
    /// <returns>A deferred sequence of raw JSON line bytes; the reader must stay undisposed while it is enumerated.</returns>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    /// <exception cref="NotSupportedException">The JSON line exceeds the supported size limit.</exception>
    public IEnumerable<JsonRawBytes> EnumerateLines(int linesToSkip)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var currentOffset = SkipBomIfAtStart(0);
        using var cursor = new LineChunkCursor(_mmap, currentOffset);
        if (!SkipLines(cursor, linesToSkip))
        {
            yield break;
        }

        while (true)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!cursor.TryReadLine(out var line))
            {
                yield break;
            }

            yield return line;
        }
    }

    private long SkipBomIfAtStart(long byteOffset)
    {
        // Skip UTF-8 BOM if present at the beginning of the file
        if (byteOffset == 0)
        {
            return _mmap.SkipUtf8Bom();
        }

        return byteOffset;
    }

    private static bool SkipLines(LineChunkCursor cursor, int linesToSkip)
    {
        var skipped = 0;

        while (skipped < linesToSkip)
        {
            if (!cursor.TrySkipLine())
            {
                return false;
            }

            skipped++;
        }

        return true;
    }

    private static void ReadRequestedLines(LineChunkCursor cursor, int linesToRead, List<JsonRawBytes> result)
    {
        var linesRead = 0;

        while (linesRead < linesToRead && cursor.TryReadLine(out var line))
        {
            result.Add(line);
            linesRead++;
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

using System.Buffers;
using Microsoft.Win32.SafeHandles;

namespace Refedle.Engine.IO.JsonLines;

/// <summary>
/// Indexes JSON Lines files by row position for efficient random access.
/// Each line in a JSON Lines file contains a complete, independent JSON object.
/// </summary>
public sealed class RowIndexer : RowIndexerBase
{
    private readonly Lock _lock = new();
    private readonly string _filePath;
    private readonly List<long> _checkpoints = [0];

    private long _totalRows;
    private long _bytesRead;

    private const int BufferSize = 1024 * 1024; // 1MB
    private const int CheckPointInterval = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="RowIndexer"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON Lines file to index.</param>
    /// <exception cref="ArgumentException">Thrown when filePath is null or whitespace.</exception>
    public RowIndexer(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Gets the path to the JSON Lines file being indexed.
    /// </summary>
    public override string FilePath => _filePath;

    /// <summary>
    /// Gets the total number of rows indexed.
    /// Updated periodically during <see cref="BuildIndex"/> (every 1000 rows) and finalized upon completion.
    /// Thread-safe via Interlocked operations.
    /// </summary>
    public override long TotalRows => Interlocked.Read(ref _totalRows);

    /// <summary>
    /// Bytes read so far. Updated atomically after each buffer read.
    /// Safe to read from any thread.
    /// </summary>
    public override long BytesRead => Interlocked.Read(ref _bytesRead);

    /// <summary>
    /// Builds the row index by scanning the entire file.
    /// Exits cooperatively when <paramref name="ct"/> is cancelled.
    /// NOT thread-safe — call once from a single background thread.
    /// <see cref="RowIndexerBase.BuildIndexCompleted"/> fires unconditionally when this method
    /// returns, regardless of whether it completed, was cancelled, or threw.
    /// </summary>
    public override void BuildIndex(CancellationToken ct = default)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            FileSize = new FileInfo(_filePath).Length;

            using SafeFileHandle handle = File.OpenHandle(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );

            long fileOffset = 0L;
            long rowCount = 0L;
            byte lastByteRead = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                int bytesRead = RandomAccess.Read(handle, buffer.AsSpan(0, BufferSize), fileOffset);

                if (bytesRead <= 0)
                {
                    break;
                }

                Interlocked.Add(ref _bytesRead, bytesRead);
                lastByteRead = buffer[bytesRead - 1];

                Span<byte> span = buffer.AsSpan(0, bytesRead);
                rowCount = ProcessBuffer(span, fileOffset, rowCount);
                fileOffset += bytesRead;
            }

            // If file doesn't end with newline, count the last line
            if (Interlocked.Read(ref _bytesRead) > 0 && lastByteRead != (byte)'\n')
            {
                rowCount++;
            }

            Interlocked.Exchange(ref _totalRows, rowCount);

            // Empty-file / sub-checkpoint guard: FirstCheckpointReached must
            // always fire so that the TaskCompletionSource in the caller does
            // not hang. Must fire AFTER TotalRows is finalised.
            OnFirstCheckpointReached();
        }
        finally
        {
            // Guarantee FirstCheckpointReached fires even on cancellation or error,
            // so the TaskCompletionSource in the caller never hangs.
            OnFirstCheckpointReached();

            ArrayPool<byte>.Shared.Return(buffer);
            OnBuildIndexCompleted();
        }
    }

    /// <summary>
    /// Gets the nearest checkpoint for random access to a target row.
    /// Returns the file byte offset and row offset from that checkpoint.
    /// This method is thread-safe.
    /// </summary>
    /// <param name="targetRow">The zero-based row index to seek to. Must be non-negative.</param>
    /// <returns>
    /// A tuple of (byteOffset, rowOffset) where byteOffset is the file position in bytes
    /// and rowOffset is the number of rows to advance from the checkpoint.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="targetRow"/> is negative.</exception>
    public override (long byteOffset, int rowOffset) GetCheckPoint(long targetRow)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetRow);

        lock (_lock)
        {
            int idealCheckPointIndex = (int)(targetRow / CheckPointInterval);
            // Clamp to the last available checkpoint (handles partial indexing or beyond-EOF requests)
            int actualCheckPointIndex = Math.Clamp(idealCheckPointIndex, 0, _checkpoints.Count - 1);
            long byteOffset = _checkpoints[actualCheckPointIndex];
            // Calculate the row number that the actual checkpoint represents
            long actualCheckPointRow = (long)actualCheckPointIndex * CheckPointInterval;
            // Calculate how many rows to skip from the actual checkpoint to reach the target row
            int rowOffset = (int)(targetRow - actualCheckPointRow);

            return (byteOffset, rowOffset);
        }
    }

    private long ProcessBuffer(
        ReadOnlySpan<byte> buffer,
        long fileOffset,
        long rowCount
    )
    {
        int position = 0;

        while (position < buffer.Length)
        {
            int index = buffer[position..].IndexOf((byte)'\n');
            if (index == -1)
            {
                break;
            }

            position += index + 1;
            rowCount++;

            if (rowCount % CheckPointInterval == 0)
            {
                Interlocked.Exchange(ref _totalRows, rowCount);

                long checkpointOffset = fileOffset + position;
                lock (_lock)
                {
                    _checkpoints.Add(checkpointOffset);
                }

                OnFirstCheckpointReached();

                OnProgressChanged(Interlocked.Read(ref _bytesRead), FileSize);
            }
        }

        return rowCount;
    }
}

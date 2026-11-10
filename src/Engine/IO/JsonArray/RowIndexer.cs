using System.Buffers;
using System.Text.Json;

namespace Refedle.Engine.IO.JsonArray;

/// <summary>
/// Indexes JSON Array files by element position for efficient random access.
/// Each top-level element in the root <c>[...]</c> array is tracked by its byte offset.
/// </summary>
/// <remarks>
/// <para>Thread-safety model:</para>
/// <list type="table">
///   <listheader><term>Member</term><description>Mechanism</description></listheader>
///   <item><term>_checkpoints writes</term><description>Lock</description></item>
///   <item><term>_checkpoints reads (GetCheckPoint)</term><description>Lock</description></item>
///   <item><term>TotalRows, BytesRead</term><description>Interlocked</description></item>
///   <item><term>Event invocations</term><description>BuildIndex thread; subscribers marshal if needed</description></item>
/// </list>
/// </remarks>
public sealed class RowIndexer : RowIndexerBase
{
    private readonly Lock _lock = new();
    private readonly string _filePath;
    private readonly List<long> _checkpoints = [];

    private long _totalRows;
    private long _bytesRead;

    private const int BufferSize = 1024 * 1024; // 1 MB — same as JsonLines
    private const int CheckPointInterval = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="RowIndexer"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON Array file to index.</param>
    /// <exception cref="ArgumentException">Thrown when filePath is null or whitespace.</exception>
    public RowIndexer(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Gets the path to the JSON Array file being indexed.
    /// </summary>
    public override string FilePath => _filePath;

    /// <summary>
    /// Gets the total number of elements indexed so far.
    /// Updated periodically during <see cref="BuildIndex"/> (every 1000 elements) and finalized upon completion.
    /// Thread-safe via Interlocked operations.
    /// </summary>
    public override long TotalRows => Interlocked.Read(ref _totalRows);

    /// <summary>
    /// Gets the total bytes read so far. Updated atomically after each buffer read.
    /// Safe to read from any thread.
    /// </summary>
    public override long BytesRead => Interlocked.Read(ref _bytesRead);

    /// <summary>
    /// Builds the element index by streaming through the entire JSON Array file.
    /// Exits cooperatively when <paramref name="ct"/> is cancelled.
    /// NOT thread-safe — call once from a single background thread.
    /// <see cref="RowIndexerBase.BuildIndexCompleted"/> fires unconditionally when this method
    /// returns, regardless of whether it completed, was cancelled, or threw.
    /// </summary>
    public override void BuildIndex(CancellationToken ct = default)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            FileSize = new FileInfo(_filePath).Length;

            using var handle = File.OpenHandle(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );

            var state = default(JsonReaderState);
            // Both offsets start after a leading UTF-8 BOM so checkpoints remain absolute
            // file offsets that ElementReader can read back from the memory map directly.
            var dataOffset = handle.GetOffsetAfterUtf8Bom();
            // Count the skipped BOM once so a completed scan reports BytesRead == FileSize.
            Interlocked.Add(ref _bytesRead, dataOffset);
            var bufferOriginFileOffset = dataOffset;
            var fileReadOffset = dataOffset;
            var remainingLen = 0;
            var elementCount = 0L;
            var currentElementStart = -1L;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                if (remainingLen == BufferSize)
                {
                    throw new NotSupportedException(
                        "JSON string value exceeds maximum supported size."
                    );
                }

                var bytesRead = RandomAccess.Read(
                    handle,
                    buffer.AsSpan(remainingLen, BufferSize - remainingLen),
                    fileReadOffset
                );

                if (bytesRead == 0)
                {
                    break;
                }

                fileReadOffset += bytesRead;
                var dataEnd = remainingLen + bytesRead;
                var isFinalBlock = fileReadOffset >= FileSize;
                Interlocked.Add(ref _bytesRead, bytesRead);

                var reader = new Utf8JsonReader(
                    buffer.AsSpan(0, dataEnd),
                    isFinalBlock,
                    state
                );

                var scanResult = ScanBufferForElements(ref reader, bufferOriginFileOffset, currentElementStart, elementCount);
                currentElementStart = scanResult.currentElementStart;
                elementCount = scanResult.elementCount;

                if (scanResult.rootArrayComplete)
                {
                    break;
                }

                ct.ThrowIfCancellationRequested();

                state = reader.CurrentState;
                var consumed = (int)reader.BytesConsumed;
                bufferOriginFileOffset += consumed;
                remainingLen = dataEnd - consumed;
                buffer.AsSpan(consumed, remainingLen).CopyTo(buffer);
            }

            Interlocked.Exchange(ref _totalRows, elementCount);
            OnFirstCheckpointReached();
        }
        finally
        {
            OnFirstCheckpointReached();
            ArrayPool<byte>.Shared.Return(buffer);
            OnBuildIndexCompleted();
        }
    }

    // Scans depth-1 tokens in the current buffer for element boundaries, recording each
    // completed element. currentElementStart/elementCount are threaded through by value since
    // an element may span a buffer boundary (state carries over to the next buffer's scan).
    private (bool rootArrayComplete, long currentElementStart, long elementCount) ScanBufferForElements(
        ref Utf8JsonReader reader,
        long bufferOriginFileOffset,
        long currentElementStart,
        long elementCount)
    {
        while (reader.Read())
        {
            if (reader.CurrentDepth == 0 && reader.TokenType == JsonTokenType.EndArray)
            {
                return (true, currentElementStart, elementCount);
            }

            if (reader.CurrentDepth != 1)
            {
                continue;
            }

            // Depth-1 tokens — element boundary detection
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                // Guard against overwriting an already-set start position.
                // In valid JSON this condition is always true, but the check
                // prevents silent data corruption if this invariant is ever violated.
                if (currentElementStart < 0)
                {
                    currentElementStart = bufferOriginFileOffset + reader.TokenStartIndex;
                }

                continue;
            }

            if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                RecordElement(currentElementStart, elementCount);
                elementCount++;
                currentElementStart = -1L;
                continue;
            }

            // Primitive token at depth 1 — self-contained element
            RecordElement(bufferOriginFileOffset + reader.TokenStartIndex, elementCount);
            elementCount++;
        }

        return (false, currentElementStart, elementCount);
    }

    private void RecordElement(long elementStart, long elementCount)
    {
        if (elementCount == 0)
        {
            lock (_lock)
            {
                _checkpoints.Add(elementStart);
            }

            return;
        }

        if (elementCount % CheckPointInterval != 0)
        {
            return;
        }

        Interlocked.Exchange(ref _totalRows, elementCount);
        lock (_lock)
        {
            _checkpoints.Add(elementStart);
        }

        OnFirstCheckpointReached();
        OnProgressChanged(Interlocked.Read(ref _bytesRead), FileSize);
    }

    /// <summary>
    /// Gets the nearest checkpoint for random access to a target element.
    /// Returns the file byte offset and row offset from that checkpoint.
    /// This method is thread-safe.
    /// </summary>
    /// <param name="targetRow">The zero-based element index to seek to. Must be non-negative.</param>
    /// <returns>
    /// A tuple of (byteOffset, rowOffset) where byteOffset is the file position in bytes
    /// and rowOffset is the number of elements to advance from the checkpoint.
    /// Returns (-1, 0) when no elements have been indexed yet.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="targetRow"/> is negative.</exception>
    public override (long byteOffset, int rowOffset) GetCheckPoint(long targetRow)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetRow);

        lock (_lock)
        {
            if (_checkpoints.Count == 0)
            {
                return (-1L, 0);
            }

            var idealCheckPointIndex = (int)(targetRow / CheckPointInterval);
            var actualCheckPointIndex = Math.Clamp(
                idealCheckPointIndex,
                0,
                _checkpoints.Count - 1
            );
            var byteOffset = _checkpoints[actualCheckPointIndex];
            var actualCheckPointRow = (long)actualCheckPointIndex * CheckPointInterval;
            var rowOffset = (int)(targetRow - actualCheckPointRow);

            return (byteOffset, rowOffset);
        }
    }
}

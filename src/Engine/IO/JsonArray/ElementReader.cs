using System.Buffers;
using System.Text.Json;

namespace Refedle.Engine.IO.JsonArray;

/// <summary>
/// Reads raw JSON element bytes from a JSON Array file given a checkpoint byte offset.
/// Uses <see cref="MmapService"/> for consistent memory-mapped random access and
/// <see cref="System.Text.Json.Utf8JsonReader"/> to locate element boundaries.
/// </summary>
public sealed class ElementReader : IDisposable
{
    private readonly MmapService _mmap;
    private bool _disposed;

    private const int BufferSize = 1024 * 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElementReader"/> class.
    /// </summary>
    /// <param name="filePath">Path to the JSON Array file.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the file cannot be memory-mapped.</exception>
    public ElementReader(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        Result<MmapService> mmapResult = MmapService.Open(filePath);
        if (mmapResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to open memory-mapped file: {mmapResult.Error}"
            );
        }

        _mmap = mmapResult.Value;
    }

    /// <summary>
    /// Reads raw JSON bytes for elements starting at <paramref name="byteOffset"/>.
    /// </summary>
    /// <param name="byteOffset">A checkpoint byte offset from <see cref="RowIndexer.GetCheckPoint"/>.</param>
    /// <param name="elementsToSkip">Number of elements to skip before collecting (non-negative).</param>
    /// <param name="elementsToFetch">Maximum elements to collect after skipping (non-negative).</param>
    /// <returns>Raw JSON bytes per element (NOT TreeNode — no App layer dependency).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for negative <paramref name="elementsToSkip"/> or <paramref name="elementsToFetch"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose"/> has been called.</exception>
    public IReadOnlyList<JsonRawBytes> ReadElements(
        long byteOffset,
        int elementsToSkip,
        int elementsToFetch
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(elementsToSkip);
        ArgumentOutOfRangeException.ThrowIfNegative(elementsToFetch);

        if (elementsToFetch == 0 || byteOffset < 0)
        {
            return [];
        }

        List<JsonRawBytes> result = [];
        result.EnsureCapacity(elementsToFetch);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            FetchElements(buffer, byteOffset, elementsToSkip, elementsToFetch, result);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return result;
    }

    private void FetchElements(
        byte[] buffer,
        long byteOffset,
        int elementsToSkip,
        int elementsToFetch,
        List<JsonRawBytes> result)
    {
        JsonReaderState state = default(JsonReaderState);
        // bufferOriginFileOffset maps buffer[0] → virtual file offset (byteOffset - 1 for synthetic '[').
        long bufferOriginFileOffset = byteOffset - 1L;
        long fileReadOffset = byteOffset;
        int remainingLen = 0;
        int elementsEncountered = 0;
        long currentElementStartFile = -1L;
        bool firstFill = true;

        while (result.Count < elementsToFetch)
        {
            if (remainingLen == BufferSize)
            {
                throw new NotSupportedException("JSON element exceeds maximum supported size.");
            }

            (int dataEnd, long newFileReadOffset) = FillBuffer(buffer, fileReadOffset, remainingLen, firstFill);
            fileReadOffset = newFileReadOffset;
            firstFill = false;

            bool isFinalBlock = fileReadOffset >= _mmap.Length;

            if (dataEnd == 0)
            {
                break;
            }

            Utf8JsonReader reader = new(buffer.AsSpan(0, dataEnd), isFinalBlock, state);
            (bool rootDone, long currentElementStartFile, int elementsEncountered) scanResult = ScanBufferForElements(
                ref reader, bufferOriginFileOffset, currentElementStartFile, elementsEncountered,
                elementsToSkip, elementsToFetch, result);
            currentElementStartFile = scanResult.currentElementStartFile;
            elementsEncountered = scanResult.elementsEncountered;

            if (scanResult.rootDone || result.Count >= elementsToFetch || isFinalBlock)
            {
                break;
            }

            state = reader.CurrentState;
            int consumed = (int)reader.BytesConsumed;
            bufferOriginFileOffset += consumed;
            remainingLen = dataEnd - consumed;

            if (remainingLen > 0 && consumed > 0)
            {
                buffer.AsSpan(consumed, remainingLen).CopyTo(buffer);
            }
        }
    }

    // buffer[0] holds a synthetic '[' on the first fill only, matching bufferOriginFileOffset's
    // -1 adjustment so token offsets resolve correctly relative to the real file.
    private (int dataEnd, long fileReadOffset) FillBuffer(byte[] buffer, long fileReadOffset, int remainingLen, bool firstFill)
    {
        if (firstFill)
        {
            buffer[0] = (byte)'[';
            long available = _mmap.Length - fileReadOffset;
            int toRead = (int)Math.Min(BufferSize - 1, Math.Max(0L, available));
            if (toRead > 0)
            {
                _mmap.Read(fileReadOffset, buffer.AsSpan(1, toRead));
            }

            return (1 + toRead, fileReadOffset + toRead);
        }

        long availableRest = _mmap.Length - fileReadOffset;
        int toReadRest = (int)Math.Min(BufferSize - remainingLen, Math.Max(0L, availableRest));
        if (toReadRest > 0)
        {
            _mmap.Read(fileReadOffset, buffer.AsSpan(remainingLen, toReadRest));
        }

        return (remainingLen + toReadRest, fileReadOffset + toReadRest);
    }

    private (bool rootDone, long currentElementStartFile, int elementsEncountered) ScanBufferForElements(
        ref Utf8JsonReader reader,
        long bufferOriginFileOffset,
        long currentElementStartFile,
        int elementsEncountered,
        int elementsToSkip,
        int elementsToFetch,
        List<JsonRawBytes> result)
    {
        while (reader.Read())
        {
            if (reader.CurrentDepth == 0 && reader.TokenType == JsonTokenType.EndArray)
            {
                return (true, currentElementStartFile, elementsEncountered);
            }

            if (reader.CurrentDepth != 1)
            {
                continue;
            }

            (currentElementStartFile, elementsEncountered) = ProcessDepth1Token(
                ref reader, bufferOriginFileOffset, currentElementStartFile, elementsEncountered, elementsToSkip, result);

            if (result.Count >= elementsToFetch)
            {
                break;
            }
        }

        return (false, currentElementStartFile, elementsEncountered);
    }

    private (long currentElementStartFile, int elementsEncountered) ProcessDepth1Token(
        ref Utf8JsonReader reader,
        long bufferOriginFileOffset,
        long currentElementStartFile,
        int elementsEncountered,
        int elementsToSkip,
        List<JsonRawBytes> result)
    {
        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            return (bufferOriginFileOffset + reader.TokenStartIndex, elementsEncountered);
        }

        if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
        {
            long endFile = bufferOriginFileOffset + reader.BytesConsumed;
            int updatedEncountered = AppendElement(currentElementStartFile, endFile, elementsEncountered, elementsToSkip, result);
            return (-1L, updatedEncountered);
        }

        // Primitive element at depth 1 (number, string, bool, null).
        int encounteredAfterPrimitive = AppendElement(
            bufferOriginFileOffset + reader.TokenStartIndex,
            bufferOriginFileOffset + reader.BytesConsumed,
            elementsEncountered, elementsToSkip, result);
        return (currentElementStartFile, encounteredAfterPrimitive);
    }

    private int AppendElement(
        long startFile,
        long endFile,
        int encountered,
        int toSkip,
        List<JsonRawBytes> result)
    {
        encountered++;
        if (encountered <= toSkip)
        {
            return encountered;
        }

        int length = (int)(endFile - startFile);
        if (length <= 0)
        {
            return encountered;
        }

        byte[] bytes = new byte[length];
        _mmap.Read(startFile, bytes);
        result.Add(bytes.AsMemory());
        return encountered;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _mmap.Dispose();
        _disposed = true;
    }
}

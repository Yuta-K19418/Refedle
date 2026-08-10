using System.Buffers;
using System.Diagnostics;
using System.Text.Json;

namespace Refedle.Engine.IO.JsonObject;

/// <summary>
/// Scans a JSON Object file and extracts all top-level key-value pairs in a single pass.
/// </summary>
public static class TopLevelScanner
{
    private const int InitialBufferSize = 1024 * 1024; // 1 MB
    private const int MaxBufferSize = 16 * 1024 * 1024; // 16 MB

    /// <summary>
    /// Scans the specified JSON Object file and returns all top-level key-value pairs.
    /// </summary>
    /// <param name="filePath">Path to the JSON Object file.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A read-only list of key-value pairs with raw JSON bytes for each value.</returns>
    public static IReadOnlyList<JsonObjectEntry> Scan(
        string filePath,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);

        try
        {
            using var handle = File.OpenHandle(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );
            var fileSize = RandomAccess.GetLength(handle);
            var state = new JsonScanState(fileSize);
            var rootCompleted = false;
            List<JsonObjectEntry> result = [];
            var keyIndex = new Dictionary<string, int>();

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                EnsureBufferCapacity(ref buffer, state.Buffer.RemainingLen);

                var bytesRead = RandomAccess.Read(
                    handle,
                    buffer.AsSpan(
                        state.Buffer.RemainingLen,
                        buffer.Length - state.Buffer.RemainingLen
                    ),
                    state.Buffer.ReadHead
                );

                if (bytesRead == 0)
                {
                    break;
                }

                state.Buffer.RecordBytesRead(bytesRead);

                var reader = new Utf8JsonReader(
                    buffer.AsSpan(0, state.Buffer.RemainingLen),
                    state.Buffer.IsFinalBlock,
                    state.Checkpoint
                );

                state.PrevCheckpoint = state.Checkpoint;

                while (reader.Read() && !rootCompleted)
                {
                    rootCompleted = ProcessToken(ref reader, ref state, buffer, result, keyIndex);
                    state.PrevCheckpoint = reader.CurrentState;
                }

                if (rootCompleted)
                {
                    break;
                }

                ct.ThrowIfCancellationRequested();

                // When tracking a nested value, rewind the reader state to the point just before
                // the opening token so the next iteration re-parses from that token with correct
                // depth context. Otherwise, use the state at the end of this iteration.
                state.Checkpoint = state.Entry.IsNested
                    ? state.RollbackCheckpoint
                    : reader.CurrentState;

                var safeConsumed = state.Entry.IsNested
                    ? (int)(state.Entry.ValueStart - state.Buffer.BufferOffset)
                    : (int)reader.BytesConsumed;
                state.Buffer.ShiftConsumed(safeConsumed, buffer);
            }

            return result.AsReadOnly();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool ProcessToken(
        ref Utf8JsonReader reader,
        ref JsonScanState state,
        byte[] buffer,
        List<JsonObjectEntry> result,
        Dictionary<string, int> keyIndex
    )
    {
        var depth = reader.CurrentDepth;

        if (depth == 0 && reader.TokenType == JsonTokenType.EndObject)
        {
            return true;
        }

        if (depth != 1)
        {
            return false;
        }

        return ProcessDepthOneToken(ref reader, ref state, buffer, result, keyIndex);
    }

    private static bool ProcessDepthOneToken(
        ref Utf8JsonReader reader,
        ref JsonScanState state,
        byte[] buffer,
        List<JsonObjectEntry> result,
        Dictionary<string, int> keyIndex
    )
    {
        if (reader.TokenType == JsonTokenType.PropertyName)
        {
            var key =
                reader.GetString()
                ?? throw new UnreachableException(
                    "PropertyName token returned a null key; this is unreachable for well-formed JSON."
                );
            state.Entry.StartEntry(key);
            return false;
        }

        // Defensive check: unreachable with well-formed JSON.
        if (state.Entry.CurrentKey.Length == 0)
        {
            return false;
        }

        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            state.Entry.RecordValueStart(
                newValueStart: state.Buffer.BufferOffset + reader.TokenStartIndex
            );
            state.RollbackCheckpoint = state.PrevCheckpoint;
            return false;
        }

        if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
        {
            var valueBufferOffset = (int)(state.Entry.ValueStart - state.Buffer.BufferOffset);
            RecordEntry(
                key: state.Entry.CurrentKey,
                buffer: buffer,
                bufferOffset: valueBufferOffset,
                length: (int)(reader.BytesConsumed - valueBufferOffset),
                result: result,
                keyIndex: keyIndex
            );
            state.Entry.ClearEntry();
            return false;
        }

        RecordEntry(
            key: state.Entry.CurrentKey,
            buffer: buffer,
            bufferOffset: (int)reader.TokenStartIndex,
            length: (int)(reader.BytesConsumed - reader.TokenStartIndex),
            result: result,
            keyIndex: keyIndex
        );
        state.Entry.ClearEntry();
        return false;
    }

    private static void RecordEntry(
        string key,
        byte[] buffer,
        int bufferOffset,
        int length,
        List<JsonObjectEntry> result,
        Dictionary<string, int> keyIndex
    )
    {
        var copy = new byte[length];
        Buffer.BlockCopy(
            src: buffer,
            srcOffset: bufferOffset,
            dst: copy,
            dstOffset: 0,
            count: length
        );
        var mem = new JsonRawBytes(copy);

        if (keyIndex.TryGetValue(key, out var idx))
        {
            result[idx] = new JsonObjectEntry(key, mem);
            return;
        }

        keyIndex[key] = result.Count;
        result.Add(new JsonObjectEntry(key, mem));
    }

    private static void EnsureBufferCapacity(ref byte[] buffer, int remainingLen)
    {
        if (remainingLen < buffer.Length)
        {
            return;
        }

        if (buffer.Length >= MaxBufferSize)
        {
            throw new NotSupportedException(
                "A single top-level value exceeds the maximum supported size (16 MB)."
            );
        }

        GrowBuffer(ref buffer, remainingLen);
    }

    private static void GrowBuffer(ref byte[] buffer, int remainingLen)
    {
        var newBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
        try
        {
            Buffer.BlockCopy(
                src: buffer,
                srcOffset: 0,
                dst: newBuffer,
                dstOffset: 0,
                count: remainingLen
            );
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(newBuffer);
            throw;
        }

        var oldBuffer = buffer;
        buffer = newBuffer;
        ArrayPool<byte>.Shared.Return(oldBuffer);
    }
}

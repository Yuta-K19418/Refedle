using System.Text.Encodings.Web;
using System.Text.Json;
using Refedle.Engine;

namespace Refedle.App.Cli;

/// <summary>
/// Writes the output as a single JSON array regardless of row count (ADR-1). The array framing
/// (<c>[</c>, inter-record <c>,</c>, <c>]</c>) is emitted as raw bytes into the staging buffer;
/// each record's object body is written by a per-record <see cref="Utf8JsonWriter.Reset()"/> and
/// flushed to the stream immediately, so the whole array is never buffered at once.
/// </summary>
internal struct JsonArrayRecordWriter : IRecordWriter
{
    private const int InitialBufferSize = 1024 * 64; // 64 KB
    private readonly BatchOutputSchema _outputSchema;
    private Stream? _stream;
    private PooledBufferWriter? _bufferWriter;
    private Utf8JsonWriter? _jsonWriter;
    private bool _isFirstRecord;
    private bool _disposed;

    public JsonArrayRecordWriter(Stream stream, BatchOutputSchema outputSchema)
    {
        _stream = stream;
        _outputSchema = outputSchema;
        _bufferWriter = new(InitialBufferSize);
        try
        {
            _jsonWriter = new(_bufferWriter, new() { SkipValidation = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
        catch
        {
            _bufferWriter.Dispose();
            throw;
        }

        _isFirstRecord = true;
        _disposed = false;
    }

    public async readonly ValueTask WriteHeaderAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_stream is null || _bufferWriter is null)
        {
            return;
        }

        WriteByte(_bufferWriter, (byte)'[');
        await _stream.WriteAsync(_bufferWriter.WrittenMemory, ct).ConfigureAwait(false);
        _bufferWriter.Clear();
    }

    public ValueTask WriteStartRecordAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_jsonWriter is null || _bufferWriter is null)
        {
            return default;
        }

        _bufferWriter.Clear();
        if (!_isFirstRecord)
        {
            WriteByte(_bufferWriter, (byte)',');
        }

        _jsonWriter.Reset();
        _jsonWriter.WriteStartObject();
        _isFirstRecord = false;
        return default;
    }

    public readonly void WriteCellData(int outputColumnIndex, CellData cell)
    {
        ThrowIfDisposed();
        if (_jsonWriter is null)
        {
            return;
        }

        JsonCellWriter.WriteCellData(_jsonWriter, _outputSchema, outputColumnIndex, cell);
    }

    public async readonly ValueTask WriteEndRecordAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_jsonWriter is null || _stream is null || _bufferWriter is null)
        {
            return;
        }

        _jsonWriter.WriteEndObject();
        _jsonWriter.Flush();

        var memory = _bufferWriter.WrittenMemory;
        if (memory.Length > 0)
        {
            await _stream.WriteAsync(memory, ct).ConfigureAwait(false);
        }
    }

    public async readonly ValueTask WriteFooterAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_stream is null || _bufferWriter is null)
        {
            return;
        }

        _bufferWriter.Clear();
        WriteByte(_bufferWriter, (byte)']');
        await _stream.WriteAsync(_bufferWriter.WrittenMemory, ct).ConfigureAwait(false);
    }

    public async readonly ValueTask FlushAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_stream is null)
        {
            return;
        }

        await _stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public readonly void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(JsonArrayRecordWriter));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _jsonWriter?.Dispose();
        _jsonWriter = null;
        _bufferWriter?.Dispose();
        _bufferWriter = null;
        _stream?.Dispose();
        _stream = null;
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_jsonWriter is not null)
        {
            await _jsonWriter.DisposeAsync().ConfigureAwait(false);
            _jsonWriter = null;
        }

        _bufferWriter?.Dispose();
        _bufferWriter = null;
        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
        }

        _disposed = true;
    }

    private static void WriteByte(PooledBufferWriter bufferWriter, byte value)
    {
        var span = bufferWriter.GetSpan(1);
        span[0] = value;
        bufferWriter.Advance(1);
    }
}

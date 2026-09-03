using System.Text.Encodings.Web;
using System.Text.Json;
using Refedle.Engine;

namespace Refedle.App.Cli;

internal struct JsonLinesRecordWriter : IRecordWriter
{
    private const int InitialBufferSize = 1024 * 64; // 64 KB
    private readonly BatchOutputSchema _outputSchema;
    private Stream? _stream;
    private PooledBufferWriter? _bufferWriter;
    private Utf8JsonWriter? _jsonWriter;
    private bool _disposed;

    public JsonLinesRecordWriter(Stream stream, BatchOutputSchema outputSchema)
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

        _disposed = false;
    }

    public readonly ValueTask WriteHeaderAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        return default;
    }

    public readonly ValueTask WriteStartRecordAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_jsonWriter is null || _bufferWriter is null)
        {
            return default;
        }

        _bufferWriter.Clear();
        _jsonWriter.Reset();
        _jsonWriter.WriteStartObject();
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

        // Add newline (using \n as standard for JSONL across platforms)
        var span = _bufferWriter.GetSpan(1);
        span[0] = (byte)'\n';
        _bufferWriter.Advance(1);

        // Write to stream
        var memory = _bufferWriter.WrittenMemory;
        if (memory.Length > 0)
        {
            await _stream.WriteAsync(memory, ct).ConfigureAwait(false);
        }
    }

    // JSON Lines has no closing frame — each line is a self-contained document.
    public readonly ValueTask WriteFooterAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        return default;
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
        ObjectDisposedException.ThrowIf(_disposed, typeof(JsonLinesRecordWriter));
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
}

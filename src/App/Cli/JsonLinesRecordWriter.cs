using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Refedle.Engine;

namespace Refedle.App.Cli;

internal partial struct JsonLinesRecordWriter : IRecordWriter
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

        if (cell.Presence == CellPresence.Missing)
        {
            return;
        }

        _jsonWriter.WritePropertyName(_outputSchema.Columns[outputColumnIndex].OutputName);

        if (cell.Presence == CellPresence.Null)
        {
            _jsonWriter.WriteNullValue();
            return;
        }

        if (cell.Presence == CellPresence.Invalid)
        {
            _jsonWriter.WriteStringValue(string.Empty);
            return;
        }

        if (cell.Encoding == CellEncoding.Raw)
        {
            _jsonWriter.WriteRawValue(cell.Value, skipInputValidation: true);
            return;
        }

        if (cell.Encoding == CellEncoding.Numeric)
        {
            writeNumericValue(_jsonWriter, cell.Value);
            return;
        }

        if (cell.Encoding == CellEncoding.Boolean)
        {
            _jsonWriter.WriteBooleanValue(bool.Parse(cell.Value));
            return;
        }

        if (cell.Encoding == CellEncoding.PlainText)
        {
            _jsonWriter.WriteStringValue(cell.Value);
            return;
        }

        throw new UnreachableException($"Unhandled CellEncoding: {cell.Encoding}");

        static void writeNumericValue(Utf8JsonWriter writer, ReadOnlySpan<char> value)
        {
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            {
                writer.WriteNumberValue(longValue);
                return;
            }

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue))
            {
                writer.WriteNumberValue(doubleValue);
                return;
            }

            throw new UnreachableException("CellEncoding.Numeric guarantees the value re-parses as long or double.");
        }
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

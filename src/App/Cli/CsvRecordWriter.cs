using System.Text;
using Refedle.Engine;

namespace Refedle.App.Cli;

internal struct CsvRecordWriter : IRecordWriter
{
    private readonly BatchOutputSchema _outputSchema;
    private readonly StringBuilder _sb;
    private StreamWriter? _writer;
    private bool _disposed;

    public CsvRecordWriter(StreamWriter writer, BatchOutputSchema outputSchema)
    {
        // Pin the line ending so batch output is byte-identical regardless of the host OS
        // (StreamWriter otherwise defaults to Environment.NewLine — CRLF on Windows).
        writer.NewLine = "\n";
        _writer = writer;
        _outputSchema = outputSchema;
        _sb = new StringBuilder();
        _disposed = false;
    }

    public async readonly ValueTask WriteHeaderAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_writer is null)
        {
            return;
        }

        for (var i = 0; i < _outputSchema.Columns.Count; i++)
        {
            if (i > 0)
            {
                await _writer.WriteAsync(",".AsMemory(), ct).ConfigureAwait(false);
            }

            var col = _outputSchema.Columns[i];
            var escaped = CsvEscaper.EscapeCsvValue(col.OutputName);
            await _writer.WriteAsync(escaped.AsMemory(), ct).ConfigureAwait(false);
        }

        await _writer.WriteLineAsync(string.Empty.AsMemory(), ct).ConfigureAwait(false);
    }

    public readonly ValueTask WriteStartRecordAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        _sb.Clear();
        return default;
    }

    public readonly void WriteCellData(int outputColumnIndex, CellData cell)
    {
        ThrowIfDisposed();
        if (outputColumnIndex > 0)
        {
            _sb.Append(',');
        }

        if (cell.Presence != CellPresence.Value)
        {
            return;
        }

        CsvEscaper.EscapeCsvValueToBuilder(cell.Value, _sb);
    }

    public async readonly ValueTask WriteEndRecordAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_writer is null)
        {
            return;
        }

        await _writer.WriteLineAsync(_sb.ToString().AsMemory(), ct).ConfigureAwait(false);
    }

    // CSV has no closing frame.
    public readonly ValueTask WriteFooterAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        return default;
    }

    public async readonly ValueTask FlushAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_writer is null)
        {
            return;
        }

        await _writer.FlushAsync(ct).ConfigureAwait(false);
    }

    public readonly void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(CsvRecordWriter));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _writer?.Dispose();
        _writer = null;
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_writer is not null)
        {
            await _writer.DisposeAsync().ConfigureAwait(false);
            _writer = null;
        }

        _disposed = true;
    }
}

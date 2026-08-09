using System.Text;
using System.Text.Json;
using Refedle.Engine;
using Refedle.Engine.IO.Json;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;

namespace Refedle.App.Cli;

internal partial struct JsonLinesRecordReader : IRecordReader
{
    private readonly RowIndexer _rowIndexer;
    private readonly Memory<byte>[] _columnNameUtf8Bytes;
    private readonly Dictionary<int, ReadOnlyMemory<byte>> _filterIndexToNameBytes;
    private readonly IReadOnlyList<Engine.Filtering.FilterSpec> _filters;
    private RowReader? _rowReader;
    private long _batchStart;
    private IReadOnlyList<JsonRawBytes> _currentBatch;
    private int _batchIndex;
    private JsonRawBytes _currentLineBytes;
    private bool _disposed;
    private readonly PooledValueBuffer _valueBuffer;

    public JsonLinesRecordReader(RowIndexer rowIndexer, RowReader rowReader, TableSchema inputSchema, BatchOutputSchema outputSchema)
    {
        _rowIndexer = rowIndexer;
        _rowReader = rowReader;

        _columnNameUtf8Bytes = [.. outputSchema.Columns
            .Select(c => Encoding.UTF8.GetBytes(c.SourceName).AsMemory())];

        _filterIndexToNameBytes = inputSchema.Columns
            .ToDictionary(c => c.ColumnIndex, c => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(c.Name));

        _filters = outputSchema.Filters;

        _batchStart = 0;
        _currentBatch = [];
        _batchIndex = -1;
        _currentLineBytes = default;
        _disposed = false;
        _valueBuffer = new PooledValueBuffer();
    }

    public ValueTask<bool> MoveNextAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_rowReader is null)
        {
            return new ValueTask<bool>(false);
        }

        while (true)
        {
            _batchIndex++;
            if (_batchIndex < _currentBatch.Count)
            {
                _currentLineBytes = _currentBatch[_batchIndex];
                if (_currentLineBytes.IsEmpty || FilterEvaluator.IsWhiteSpace(_currentLineBytes.Span))
                {
                    continue;
                }

                return new ValueTask<bool>(true);
            }

            if (_batchStart >= _rowIndexer.TotalRows)
            {
                return new ValueTask<bool>(false);
            }

            ct.ThrowIfCancellationRequested();

            var (byteOffset, rowOffset) = _rowIndexer.GetCheckPoint(_batchStart);
            var linesToRead = (int)Math.Min(1000, _rowIndexer.TotalRows - _batchStart);

            _currentBatch = _rowReader.ReadLines(byteOffset, rowOffset, linesToRead);
            _batchStart += linesToRead;
            _batchIndex = -1;
        }
    }

    public readonly void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public readonly bool EvaluateFilters()
    {
        ThrowIfDisposed();
        return FilterEvaluator.EvaluateJsonFilters(_currentLineBytes, _filters, _filterIndexToNameBytes);
    }

    public readonly CellData GetCellData(int outputColumnIndex)
    {
        ThrowIfDisposed();

        var columnNameUtf8 = _columnNameUtf8Bytes[outputColumnIndex].Span;

        try
        {
            var reader = new Utf8JsonReader(_currentLineBytes.Span);

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return new CellData([], CellPresence.Invalid);
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                if (!reader.ValueTextEquals(columnNameUtf8))
                {
                    reader.Skip();
                    continue;
                }

                if (!reader.Read())
                {
                    return new CellData([], CellPresence.Invalid);
                }

                return ReadPropertyValue(reader, _currentLineBytes);
            }

            return new CellData([], CellPresence.Missing);
        }
        catch (JsonException)
        {
            return new CellData([], CellPresence.Invalid);
        }
    }

    // Split out to stay under the Sonar cyclomatic-complexity limit (S1541). Passed by value,
    // not by ref, so it owns a copy isolated from the caller's state (ref also fails to
    // compile: CS8168/CS8347); the resulting small, stack-only copy per call is an accepted cost.
    private static CellData ReadPropertyValue(Utf8JsonReader reader, JsonRawBytes containingBytes)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => new CellData([], CellPresence.Null),
            JsonTokenType.Number =>
                new CellData(Encoding.UTF8.GetString(reader.ValueSpan), CellPresence.Value, CellEncoding.Raw),
            JsonTokenType.StartObject or JsonTokenType.StartArray =>
                new CellData(
                    Encoding.UTF8.GetString(JsonByteExtractor.ExtractValueBytes(ref reader, containingBytes).Span),
                    CellPresence.Value,
                    CellEncoding.Raw),
            JsonTokenType.String =>
                new CellData(reader.GetString(), CellPresence.Value, CellEncoding.PlainText),
            JsonTokenType.True => new CellData("true", CellPresence.Value, CellEncoding.Boolean),
            JsonTokenType.False => new CellData("false", CellPresence.Value, CellEncoding.Boolean),
            _ => new CellData([], CellPresence.Invalid),
        };
    }

    // Step 2 scaffolds: defined now and wired into ReadPropertyValue's branches in Step 2
    // (see docs/design_jsonlines_cell_value_zero_alloc.md). Left uncalled in Step 1 so the
    // existing string-backed branches keep their behavior and existing tests do not regress.
#pragma warning disable IDE0051 // Wired into ReadPropertyValue's branches in Step 2
    private readonly CellData NumberToCellData(Utf8JsonReader reader)
    {
        throw new NotImplementedException();
    }

    private readonly CellData ObjectOrArrayToCellData(Utf8JsonReader reader, JsonRawBytes containingBytes)
    {
        throw new NotImplementedException();
    }

    private readonly CellData StringToCellData(Utf8JsonReader reader)
    {
        throw new NotImplementedException();
    }
#pragma warning restore IDE0051

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _rowReader?.Dispose();
        _rowReader = null;

        _valueBuffer.Dispose();

        _disposed = true;
    }
}

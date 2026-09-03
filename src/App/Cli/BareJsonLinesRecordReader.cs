using System.Text;
using Refedle.Engine;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO.Json;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Utilities;

namespace Refedle.App.Cli;

/// <summary>
/// Straight-line JSON Lines reader: one line per row, no DrillDown. The non-DrillDown arm of
/// the <see cref="JsonLinesRecordReader"/> dispatch struct (ADR-6).
/// </summary>
internal struct BareJsonLinesRecordReader : IRecordReader
{
    private readonly RowIndexer _rowIndexer;
    private readonly Memory<byte>[] _columnNameUtf8Bytes;
    private readonly Dictionary<int, ReadOnlyMemory<byte>> _filterIndexToNameBytes;
    private readonly IReadOnlyList<BatchFilterSpec> _filters;
    private RowReader? _rowReader;
    private long _batchStart;
    private IReadOnlyList<JsonRawBytes> _currentBatch;
    private int _batchIndex;
    private JsonRawBytes _currentLineBytes;
    private bool _disposed;
    private readonly PooledValueBuffer _valueBuffer;

    // _rowReader is non-null on construction; it becomes null only after Dispose, matching
    // the post-dispose fail-fast idiom of the sibling Csv/JsonLines readers and writers.
    public BareJsonLinesRecordReader(RowIndexer rowIndexer, RowReader rowReader, IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema)
    {
        _rowIndexer = rowIndexer;
        _rowReader = rowReader;

        _columnNameUtf8Bytes = [.. outputSchema.Columns
            .Select(c => Encoding.UTF8.GetBytes(c.SourceName).AsMemory())];

        _filterIndexToNameBytes = new Dictionary<int, ReadOnlyMemory<byte>>(inputColumnNames.Count);
        for (var i = 0; i < inputColumnNames.Count; i++)
        {
            _filterIndexToNameBytes[i] = Encoding.UTF8.GetBytes(inputColumnNames[i]).AsMemory();
        }

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
                if (_currentLineBytes.IsEmpty || StringUtility.IsWhiteSpace(_currentLineBytes.Span))
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
        ObjectDisposedException.ThrowIf(_disposed, typeof(BareJsonLinesRecordReader));
    }

    public readonly bool EvaluateFilters()
    {
        ThrowIfDisposed();

        foreach (var filter in _filters)
        {
            if (!_filterIndexToNameBytes.TryGetValue(filter.SourceColumnIndex, out var sourceColNameBytes))
            {
                continue;
            }

            var value = JsonObjectCellExtractor.ExtractCell(_currentLineBytes.Span, sourceColNameBytes.Span);

            if (value == "<null>" || value == "<error>")
            {
                return false;
            }

            if (!FilterEvaluator.EvaluateFilter(value.AsSpan(), filter))
            {
                return false;
            }
        }

        return true;
    }

    public readonly CellData GetCellData(int outputColumnIndex)
    {
        ThrowIfDisposed();

        return JsonObjectCellReader.ReadCell(
            _currentLineBytes, _columnNameUtf8Bytes[outputColumnIndex].Span, _valueBuffer);
    }

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

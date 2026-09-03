using System.Text;
using Refedle.Engine;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.Json;

namespace Refedle.App.Cli;

/// <summary>
/// Full Aggregation DrillDown record reader: streams the input through
/// <typeparamref name="TBatchSourceReader"/> in bounded batches, extracting each batch's
/// DrillDown rows via <see cref="FullAggregationRowExtractor.ExtractRows"/> and yielding them
/// one at a time — rows are never all held at once, unlike the TUI's FullAggregationScanner
/// (ADR-3). Generic over the batch source so JSON Lines DrillDown (Phase 6) reuses this logic
/// without struct inheritance or boxing.
/// </summary>
internal struct FullAggregationRecordReader<TBatchSourceReader> : IRecordReader
    where TBatchSourceReader : struct, IBatchSourceReader
{
    private const int BatchSize = 1000;

    private readonly RowIndexerBase _rowIndexer;
    private readonly TBatchSourceReader _batchSource;
    private readonly IReadOnlyList<KeyPathSegment> _keyPath;
    private readonly Memory<byte>[] _columnNameUtf8Bytes;
    private readonly Dictionary<int, ReadOnlyMemory<byte>> _filterIndexToNameBytes;
    private readonly IReadOnlyList<BatchFilterSpec> _filters;
    private IReadOnlyList<FocusedTableRow> _currentRows;
    private long _batchStart;
    private int _rowIndex;
    private JsonRawBytes _currentRowBytes;
    private bool _disposed;
    private readonly PooledValueBuffer _valueBuffer;

    public FullAggregationRecordReader(
        RowIndexerBase rowIndexer,
        TBatchSourceReader batchSource,
        IReadOnlyList<KeyPathSegment> keyPath,
        IReadOnlyList<string> inputColumnNames,
        BatchOutputSchema outputSchema)
    {
        _rowIndexer = rowIndexer;
        _batchSource = batchSource;
        _keyPath = keyPath;

        _columnNameUtf8Bytes = [.. outputSchema.Columns
            .Select(c => Encoding.UTF8.GetBytes(c.SourceName).AsMemory())];

        _filterIndexToNameBytes = new Dictionary<int, ReadOnlyMemory<byte>>(inputColumnNames.Count);
        for (var i = 0; i < inputColumnNames.Count; i++)
        {
            _filterIndexToNameBytes[i] = Encoding.UTF8.GetBytes(inputColumnNames[i]).AsMemory();
        }

        _filters = outputSchema.Filters;

        _currentRows = [];
        _batchStart = 0;
        _rowIndex = -1;
        _currentRowBytes = default;
        _disposed = false;
        _valueBuffer = new PooledValueBuffer();
    }

    public ValueTask<bool> MoveNextAsync(CancellationToken ct)
    {
        ThrowIfDisposed();

        while (true)
        {
            _rowIndex++;
            if (_rowIndex < _currentRows.Count)
            {
                _currentRowBytes = _currentRows[_rowIndex].Bytes;
                return new ValueTask<bool>(true);
            }

            if (_batchStart >= _rowIndexer.TotalRows)
            {
                return new ValueTask<bool>(false);
            }

            ct.ThrowIfCancellationRequested();

            var (byteOffset, rowOffset) = _rowIndexer.GetCheckPoint(_batchStart);
            var recordsToFetch = (int)Math.Min(BatchSize, _rowIndexer.TotalRows - _batchStart);
            var recordBatch = _batchSource.ReadBatch(byteOffset, rowOffset, recordsToFetch);
            _currentRows = FullAggregationRowExtractor.ExtractRows(recordBatch, _keyPath);
            _batchStart += recordsToFetch;
            _rowIndex = -1;
        }
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

            var value = JsonObjectCellExtractor.ExtractCell(_currentRowBytes.Span, sourceColNameBytes.Span);

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
            _currentRowBytes, _columnNameUtf8Bytes[outputColumnIndex].Span, _valueBuffer);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _batchSource.Dispose();
        _valueBuffer.Dispose();

        _disposed = true;
    }

    private readonly void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(FullAggregationRecordReader<TBatchSourceReader>));
    }
}

using System.Text;
using Refedle.Engine;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO.Json;

namespace Refedle.App.Cli;

/// <summary>
/// Single DrillDown record reader for JSON Object input: wraps the child rows the factory
/// already resolved in memory via <see cref="Engine.IO.DrillDown.KeyPathNodeResolver"/> and
/// <see cref="Engine.IO.DrillDown.DrillDownSchemaExtractor"/> (ADR-4 — a Single DrillDown
/// node is bounded, so no streaming infrastructure is needed). No further I/O happens.
/// </summary>
internal struct JsonObjectRecordReader : IRecordReader
{
    private readonly IReadOnlyList<JsonRawBytes> _rows;
    private readonly Memory<byte>[] _columnNameUtf8Bytes;
    private readonly Dictionary<int, ReadOnlyMemory<byte>> _filterIndexToNameBytes;
    private readonly IReadOnlyList<BatchFilterSpec> _filters;
    private int _rowIndex;
    private JsonRawBytes _currentRowBytes;
    private bool _disposed;
    private readonly PooledValueBuffer _valueBuffer;

    public JsonObjectRecordReader(
        IReadOnlyList<JsonRawBytes> rows,
        IReadOnlyList<string> inputColumnNames,
        BatchOutputSchema outputSchema)
    {
        _rows = rows;

        _columnNameUtf8Bytes = [.. outputSchema.Columns
            .Select(c => Encoding.UTF8.GetBytes(c.SourceName).AsMemory())];

        _filterIndexToNameBytes = new Dictionary<int, ReadOnlyMemory<byte>>(inputColumnNames.Count);
        for (var i = 0; i < inputColumnNames.Count; i++)
        {
            _filterIndexToNameBytes[i] = Encoding.UTF8.GetBytes(inputColumnNames[i]).AsMemory();
        }

        _filters = outputSchema.Filters;

        _rowIndex = -1;
        _currentRowBytes = default;
        _disposed = false;
        _valueBuffer = new PooledValueBuffer();
    }

    public ValueTask<bool> MoveNextAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        _rowIndex++;
        if (_rowIndex >= _rows.Count)
        {
            return new ValueTask<bool>(false);
        }

        _currentRowBytes = _rows[_rowIndex];
        return new ValueTask<bool>(true);
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

        _valueBuffer.Dispose();

        _disposed = true;
    }

    private readonly void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(JsonObjectRecordReader));
    }
}

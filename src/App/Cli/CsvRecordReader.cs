using nietras.SeparatedValues;
using Refedle.Engine;
using Refedle.Engine.Filtering;

namespace Refedle.App.Cli;

internal struct CsvRecordReader : IRecordReader
{
    private readonly int[] _outputToSourceIndexMap;
    private readonly IReadOnlyList<FilterSpec> _filters;
    private SepReader? _reader;
    private bool _disposed;

    public CsvRecordReader(SepReader reader, BatchOutputSchema outputSchema)
    {
        _reader = reader;

        var header = _reader.Header;
        var sourceNameToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < header.ColNames.Count; i++)
        {
            sourceNameToIndex[header.ColNames[i]] = i;
        }

        _outputToSourceIndexMap = new int[outputSchema.Columns.Count];
        for (var i = 0; i < outputSchema.Columns.Count; i++)
        {
            var col = outputSchema.Columns[i];
            _outputToSourceIndexMap[i] = sourceNameToIndex.TryGetValue(col.SourceName, out var idx) ? idx : -1;
        }

        _filters = outputSchema.Filters;
        _disposed = false;
    }

    public readonly ValueTask<bool> MoveNextAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_reader is null)
        {
            return new ValueTask<bool>(false);
        }

        return _reader.MoveNextAsync(ct);
    }

    public readonly void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(CsvRecordReader));
    }

    public readonly bool EvaluateFilters()
    {
        ThrowIfDisposed();
        if (_reader is null)
        {
            return false;
        }

        return FilterEvaluator.EvaluateCsvFilters(_reader.Current, _filters);
    }

    public readonly CellData GetCellData(int outputColumnIndex)
    {
        ThrowIfDisposed();
        if (_reader is null)
        {
            return new CellData([], CellPresence.Value);
        }

        var sourceIndex = _outputToSourceIndexMap[outputColumnIndex];
        var value = sourceIndex < 0 ? [] : _reader.Current[sourceIndex].Span;
        return new CellData(value, CellPresence.Value, CellEncodingClassifier.Classify(value));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _reader?.Dispose();
        _reader = null;
        _disposed = true;
    }
}

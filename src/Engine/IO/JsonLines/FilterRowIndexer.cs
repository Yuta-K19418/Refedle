using Refedle.Engine.Filtering;
using Refedle.Engine.IO.Json;

namespace Refedle.Engine.IO.JsonLines;

/// <summary>
/// JSON Lines-specific implementation of <see cref="IFilterRowIndexer"/>.
/// Uses <see cref="RowIndexer"/> for row checkpoints and a dedicated
/// <see cref="RowReader"/> (separate from the display cache) for sequential
/// row scanning, limiting total file I/O to approximately one full pass.
/// </summary>
public sealed class FilterRowIndexer : IFilterRowIndexer
{
    private readonly IRowIndexer _indexer;
    private readonly string _filePath;
    private readonly IReadOnlyList<byte[]> _columnNamesUtf8;
    private readonly IReadOnlyList<FilterSpec> _filterSpecs;
    private readonly Lock _lock = new();
    private readonly List<int> _matchedRows = [];
    private int _totalMatchedRows;

    /// <summary>
    /// Initializes a new instance of <see cref="FilterRowIndexer"/>.
    /// </summary>
    /// <param name="indexer">Completed row indexer for the JSON Lines source file.</param>
    /// <param name="filePath">Path to the JSON Lines source file.</param>
    /// <param name="columnNamesUtf8">UTF-8 encoded column names for <see cref="JsonObjectCellExtractor"/> lookups.</param>
    /// <param name="filterSpecs">Resolved filter specifications to apply.</param>
    public FilterRowIndexer(
        IRowIndexer indexer,
        string filePath,
        IReadOnlyList<byte[]> columnNamesUtf8,
        IReadOnlyList<FilterSpec> filterSpecs
    )
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(columnNamesUtf8);
        ArgumentNullException.ThrowIfNull(filterSpecs);
        _indexer = indexer;
        _filePath = filePath;
        _columnNamesUtf8 = columnNamesUtf8;
        _filterSpecs = filterSpecs;
    }

    /// <inheritdoc/>
    public int TotalMatchedRows
    {
        get
        {
            lock (_lock)
            {
                return _totalMatchedRows;
            }
        }
    }

    /// <inheritdoc/>
    public int GetSourceRow(int filteredRow)
    {
        lock (_lock)
        {
            return filteredRow < 0 || filteredRow >= _matchedRows.Count ? -1 : _matchedRows[filteredRow];
        }
    }

    /// <inheritdoc/>
    public async Task BuildIndexAsync(CancellationToken ct)
    {
        var totalRows = (int)_indexer.TotalRows;

        if (totalRows <= 0)
        {
            return;
        }

        using var reader = new RowReader(_filePath);

        const int batchSize = 1000;
        var processed = 0;
        var sourceRow = 0;

        while (processed < totalRows)
        {
            ct.ThrowIfCancellationRequested();

            var rowsToRead = Math.Min(batchSize, totalRows - processed);
            var (byteOffset, rowOffset) = _indexer.GetCheckPoint(sourceRow);
            var lines = reader.ReadLines(byteOffset, rowOffset, rowsToRead);

            if (lines.Count == 0)
            {
                break;
            }

            for (var i = 0; i < lines.Count; i++)
            {
                if (MatchesAllFilters(lines[i].Span))
                {
                    lock (_lock)
                    {
                        _matchedRows.Add(sourceRow + i);
                        _totalMatchedRows++;
                    }
                }
            }

            processed += lines.Count;
            sourceRow += lines.Count;

            await Task.Yield();
        }
    }

    private bool MatchesAllFilters(ReadOnlySpan<byte> lineBytes)
    {
        foreach (var spec in _filterSpecs)
        {
            var colIdx = spec.SourceColumnIndex;
            var colNameUtf8 =
                colIdx < _columnNamesUtf8.Count ? _columnNamesUtf8[colIdx].AsSpan() : ReadOnlySpan<byte>.Empty;
            var rawValue = colNameUtf8.IsEmpty
                ? []
                : JsonObjectCellExtractor.ExtractCell(lineBytes, colNameUtf8).AsSpan();

            if (!FilterEvaluator.EvaluateFilter(rawValue, spec))
            {
                return false;
            }
        }

        return true;
    }
}

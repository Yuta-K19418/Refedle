using Refedle.Engine.Filtering;

namespace Refedle.Engine.IO.Csv;

/// <summary>
/// CSV-specific implementation of <see cref="IFilterRowIndexer"/>.
/// Uses <see cref="DataRowIndexer"/> for row checkpoints and a dedicated
/// <see cref="DataRowReader"/> (separate from the display cache) for sequential
/// row scanning, limiting total file I/O to approximately one full pass.
/// </summary>
public sealed class FilterRowIndexer : IFilterRowIndexer
{
    private readonly IRowIndexer _indexer;
    private readonly int _sourceColumnCount;
    private readonly IReadOnlyList<FilterSpec> _filterSpecs;
    private readonly Lock _lock = new();
    private readonly List<int> _matchedRows = [];
    private int _totalMatchedRows;
    private readonly ScanProgressThrottle _progressThrottle = new();

    /// <summary>
    /// Initializes a new instance of <see cref="FilterRowIndexer"/>.
    /// </summary>
    /// <param name="indexer">Completed row indexer for the CSV source file.</param>
    /// <param name="sourceColumnCount">Number of columns in the source schema.</param>
    /// <param name="filterSpecs">Resolved filter specifications to apply.</param>
    public FilterRowIndexer(
        IRowIndexer indexer,
        int sourceColumnCount,
        IReadOnlyList<FilterSpec> filterSpecs
    )
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(filterSpecs);
        _indexer = indexer;
        _sourceColumnCount = sourceColumnCount;
        _filterSpecs = filterSpecs;
    }

    /// <summary>
    /// Raised on the scanning task with the scan position as <c>(scannedRows, totalRows)</c>.
    /// Throttled by <see cref="ScanProgressThrottle"/> while the scan runs; the final batch always
    /// raises it with the full row count, so completion observers see the final state.
    /// </summary>
    public event Action<int, int>? ProgressChanged;

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
            if (filteredRow < 0 || filteredRow >= _matchedRows.Count)
            {
                return -1;
            }

            return _matchedRows[filteredRow];
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

        using var reader = new DataRowReader(_indexer.FilePath, _sourceColumnCount);
        var (startByteOffset, startRowOffset) = _indexer.GetCheckPoint(0);

        if (startByteOffset < 0)
        {
            return;
        }

        const int batchSize = 1000;
        var processed = 0;
        var readOffset = startByteOffset;
        var rowsToSkip = startRowOffset;
        var sourceRow = 0;

        while (processed < totalRows)
        {
            ct.ThrowIfCancellationRequested();

            var rowsToRead = Math.Min(batchSize, totalRows - processed);
            var rows = reader.ReadRows(readOffset, rowsToSkip, rowsToRead);

            if (rows.Count == 0)
            {
                break;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                if (MatchesAllFilters(rows[i]))
                {
                    lock (_lock)
                    {
                        _matchedRows.Add(sourceRow + i);
                        _totalMatchedRows++;
                    }
                }
            }

            processed += rows.Count;
            sourceRow += rows.Count;

            // Advance to next batch using checkpoint for the next batch start row
            var (nextByteOffset, nextRowOffset) = _indexer.GetCheckPoint(sourceRow);
            readOffset = nextByteOffset;
            rowsToSkip = nextRowOffset;

            await Task.Yield();
            // Re-check after the async hop: a canceled scan must not report a further position.
            ct.ThrowIfCancellationRequested();
            OnProgressChanged(processed, totalRows);
        }
    }

    private void OnProgressChanged(int scannedRows, int totalRows)
    {
        if (!_progressThrottle.ShouldNotify(scannedRows, totalRows))
        {
            return;
        }

        ProgressChanged?.Invoke(scannedRows, totalRows);
    }

    private bool MatchesAllFilters(CsvDataRow csvRow)
    {
        foreach (var spec in _filterSpecs)
        {
            var colIdx = spec.SourceColumnIndex;
            var rawValue =
                colIdx < csvRow.Count && !csvRow[colIdx].IsEmpty
                    ? csvRow[colIdx].Span
                    : [];

            if (!FilterEvaluator.EvaluateFilter(rawValue, spec))
            {
                return false;
            }
        }

        return true;
    }
}

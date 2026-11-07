using System.Diagnostics;
using System.Globalization;
using Refedle.Engine.Filtering;

namespace Refedle.App.Tui.UI.Views;

/// <summary>
/// In-memory implementation of <see cref="IFilterRowIndexer"/> for DrillDown results backed by a
/// fully materialized <see cref="FocusedTableSource"/>-shaped <see cref="IExtendedTableSource"/>.
/// Column 0 of the source is the <c>"#"</c> pseudo column, so a spec's
/// <see cref="FilterSpec.SourceColumnIndex"/> maps to source column <c>SourceColumnIndex + 1</c>.
/// </summary>
internal sealed class FocusedTableFilterRowIndexer : IFilterRowIndexer
{
    private const int BatchSize = 1000;

    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(100);

    private readonly IExtendedTableSource _source;
    private readonly IReadOnlyList<FilterSpec> _filterSpecs;
    private readonly int _revealThreshold;
    private readonly TaskCompletionSource _thresholdReached =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _lock = new();
    private readonly List<int> _matchedRows = [];
    private long _lastProgressTimestamp;

    /// <summary>
    /// Raised on the scanning task with the scan position as <c>(scannedRows, totalRows)</c>.
    /// Throttled to <see cref="ProgressInterval"/> while the scan runs; the final batch always
    /// raises it with the full row count, so completion observers see the final state.
    /// </summary>
    public event Action<int, int>? ProgressChanged;

    /// <summary>
    /// Completes the moment the matched-row count reaches the reveal threshold, mid-scan.
    /// Canceled when <see cref="BuildIndexAsync"/> ends first (success, fault, or cancellation),
    /// so awaiting it via <c>Task.WhenAny</c> never hangs or leaks the cancellation.
    /// </summary>
    public Task ThresholdReached => _thresholdReached.Task;

    /// <summary>
    /// Initializes a new instance of <see cref="FocusedTableFilterRowIndexer"/>.
    /// </summary>
    /// <param name="source">The source whose rows are scanned; column 0 is the <c>"#"</c> hash column.</param>
    /// <param name="filterSpecs">Resolved filter specifications to apply (AND semantics).</param>
    /// <param name="revealThreshold">
    /// Matched rows required before <see cref="ThresholdReached"/> completes.
    /// </param>
    public FocusedTableFilterRowIndexer(
        IExtendedTableSource source,
        IReadOnlyList<FilterSpec> filterSpecs,
        int revealThreshold)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(filterSpecs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revealThreshold);
        _source = source;
        _filterSpecs = filterSpecs;
        _revealThreshold = revealThreshold;
    }

    /// <inheritdoc/>
    public int TotalMatchedRows
    {
        get
        {
            lock (_lock)
            {
                return _matchedRows.Count;
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
        try
        {
            var totalRows = (int)_source.TotalRows;

            for (var batchStart = 0; batchStart < totalRows; batchStart += BatchSize)
            {
                ct.ThrowIfCancellationRequested();

                var batchEnd = Math.Min(batchStart + BatchSize, totalRows);
                for (var row = batchStart; row < batchEnd; row++)
                {
                    if (MatchesAllFilters(row))
                    {
                        lock (_lock)
                        {
                            _matchedRows.Add(row);
                            if (_matchedRows.Count >= _revealThreshold)
                            {
                                _thresholdReached.TrySetResult();
                            }
                        }
                    }
                }

                await Task.Yield();
                // Re-check after the async hop: a canceled scan must not report a further position.
                ct.ThrowIfCancellationRequested();
                OnProgressChanged(batchEnd, totalRows);
            }

            // The final check also covers the empty source, where the loop body never runs.
            ct.ThrowIfCancellationRequested();
        }
        finally
        {
            // A scan that never reached the threshold must not leave its wait pending,
            // whatever way it ended; after TrySetResult this cancel is a no-op.
            _thresholdReached.TrySetCanceled(ct);
        }
    }

    private void OnProgressChanged(int scannedRows, int totalRows)
    {
        // The timestamp starts at 0 (the stopwatch epoch), so the first batch always notifies.
        var now = Stopwatch.GetTimestamp();
        if (scannedRows < totalRows && Stopwatch.GetElapsedTime(_lastProgressTimestamp) < ProgressInterval)
        {
            return;
        }

        _lastProgressTimestamp = now;
        ProgressChanged?.Invoke(scannedRows, totalRows);
    }

    private bool MatchesAllFilters(int row)
    {
        foreach (var spec in _filterSpecs)
        {
            var rawValue = Convert.ToString(
                _source[row, spec.SourceColumnIndex + 1],
                CultureInfo.InvariantCulture
            ) ?? string.Empty;

            if (!FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec))
            {
                return false;
            }
        }

        return true;
    }
}

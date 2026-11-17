using System.Globalization;
using Refedle.Engine.Filtering;

namespace Refedle.App.Tui.UI.Views;

/// <summary>
/// In-memory implementation of <see cref="IFilterRowIndexer"/> for DrillDown results backed by a
/// fully materialized <see cref="FocusedTableSource"/>-shaped <see cref="IExtendedTableSource"/>.
/// Column 0 of the source is the <c>"#"</c> pseudo column, so a spec's
/// <see cref="FilterSpec.SourceColumnIndex"/> maps to source column <c>SourceColumnIndex + 1</c>.
/// </summary>
internal sealed class FocusedTableFilterRowIndexer(
    IExtendedTableSource source,
    IReadOnlyList<FilterSpec> filterSpecs,
    int revealThreshold) : IFilterRowIndexer
{
    private const int BatchSize = 1000;

    private readonly IExtendedTableSource _source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly IReadOnlyList<FilterSpec> _filterSpecs =
        filterSpecs ?? throw new ArgumentNullException(nameof(filterSpecs));
    private readonly int _revealThreshold =
        revealThreshold > 0 ? revealThreshold : throw new ArgumentOutOfRangeException(nameof(revealThreshold));
    private readonly TaskCompletionSource _thresholdReached =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _lock = new();
    private readonly List<int> _matchedRows = [];
    private readonly ScanProgressThrottle _progressThrottle = new();

    /// <summary>
    /// Raised on the scanning task with the scan position as <c>(scannedRows, totalRows)</c>.
    /// Throttled by <see cref="ScanProgressThrottle"/> while the scan runs; the final batch always
    /// raises it with the full row count, so completion observers see the final state.
    /// </summary>
    public event Action<int, int>? ProgressChanged;

    /// <summary>
    /// Completes the moment the matched-row count reaches the reveal threshold, mid-scan.
    /// Canceled when <see cref="BuildIndexAsync"/> ends first (success, fault, or cancellation),
    /// so awaiting it via <c>Task.WhenAny</c> never hangs or leaks the cancellation.
    /// </summary>
    public Task ThresholdReached => _thresholdReached.Task;

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
            int totalRows = (int)_source.TotalRows;
            await ScanBatchesAsync(totalRows, ct).ConfigureAwait(false);

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

    private async Task ScanBatchesAsync(int totalRows, CancellationToken ct)
    {
        for (int batchStart = 0; batchStart < totalRows; batchStart += BatchSize)
        {
            ct.ThrowIfCancellationRequested();
            int batchEnd = Math.Min(batchStart + BatchSize, totalRows);
            await ScanBatchAsync(batchStart, batchEnd, totalRows, ct).ConfigureAwait(false);
        }
    }

    private async Task ScanBatchAsync(int batchStart, int batchEnd, int totalRows, CancellationToken ct)
    {
        for (int row = batchStart; row < batchEnd; row++)
        {
            RecordRowIfMatch(row);
        }

        await Task.Yield();
        // Re-check after the async hop: a canceled scan must not report a further position.
        ct.ThrowIfCancellationRequested();
        OnProgressChanged(batchEnd, totalRows);
    }

    private void RecordRowIfMatch(int row)
    {
        if (!MatchesAllFilters(row))
        {
            return;
        }

        lock (_lock)
        {
            _matchedRows.Add(row);
            SignalThresholdIfReached();
        }
    }

    private void SignalThresholdIfReached()
    {
        if (_matchedRows.Count < _revealThreshold)
        {
            return;
        }

        _thresholdReached.TrySetResult();
    }

    private void OnProgressChanged(int scannedRows, int totalRows)
    {
        if (!_progressThrottle.ShouldNotify(scannedRows, totalRows))
        {
            return;
        }

        ProgressChanged?.Invoke(scannedRows, totalRows);
    }

    private bool MatchesAllFilters(int row)
    {
        foreach (FilterSpec spec in _filterSpecs)
        {
            string rawValue = Convert.ToString(
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

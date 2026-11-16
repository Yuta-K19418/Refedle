namespace Refedle.Engine.Filtering;

/// <summary>
/// Builds and exposes a pre-computed index of source row positions that satisfy
/// all active <see cref="FilterSpec"/>s.
/// Implementations scan the source file once in the background; the index grows
/// atomically so the TUI can render already-confirmed rows immediately.
/// </summary>
public interface IFilterRowIndexer
{
    /// <summary>
    /// Raised on the scanning task with the scan position as <c>(scannedRows, totalRows)</c>.
    /// Implementations throttle the notifications while the scan runs; the final batch always
    /// raises it with the full row count, so completion observers see the final state.
    /// </summary>
    event Action<int, int>? ProgressChanged;

    /// <summary>
    /// Number of source rows confirmed to match all filters so far.
    /// Updated atomically; safe to read from the UI thread while
    /// <see cref="BuildIndexAsync"/> runs.
    /// </summary>
    int TotalMatchedRows { get; }

    /// <summary>
    /// Returns the source row index for a given filtered row index.
    /// Returns <c>-1</c> if the scan has not yet reached this filtered row.
    /// </summary>
    int GetSourceRow(int filteredRow);

    /// <summary>
    /// Scans all source rows sequentially and builds the matched-row index.
    /// Must be called once on a background task after construction.
    /// </summary>
    Task BuildIndexAsync(CancellationToken ct);
}

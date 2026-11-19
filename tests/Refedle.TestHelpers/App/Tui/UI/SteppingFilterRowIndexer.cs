using Refedle.Engine.Filtering;

namespace Refedle.TestHelpers.App.Tui.UI;

/// <summary>
/// Indexer double whose progress the test raises manually, so tick handling is observed
/// without a running background build.
/// </summary>
internal sealed class SteppingFilterRowIndexer : IFilterRowIndexer
{
    public event Action<int, int>? ProgressChanged;

    public int TotalMatchedRows => 0;

    public int GetSourceRow(int filteredRow) => -1;

    public Task BuildIndexAsync(CancellationToken ct) => Task.CompletedTask;

    public void RaiseProgress(int scannedRows, int totalRows) =>
        ProgressChanged?.Invoke(scannedRows, totalRows);
}

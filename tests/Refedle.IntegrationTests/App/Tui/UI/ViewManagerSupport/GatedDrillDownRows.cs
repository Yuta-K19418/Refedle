using Refedle.Engine.IO.DrillDown;

namespace Refedle.IntegrationTests.App.Tui.UI.ViewManagerSupport;

/// <summary>
/// Row list whose read of one configured row blocks until released, so ViewManager-level
/// tests can hold the filter-scan worker mid-batch. Every cell read of a row — the "#"
/// column and data columns alike — goes through this indexer, and reads are counted so a
/// test can prove the worker stopped reading further batches.
/// </summary>
internal sealed class GatedDrillDownRows(FocusedTableRow[] rows, int gateRowIndex)
    : IReadOnlyList<FocusedTableRow>, IDisposable
{
    private readonly ManualResetEventSlim _gate = new(false);
    private long _readCount;

    public TaskCompletionSource GateReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Count => rows.Length;

    public long ReadCount => Interlocked.Read(ref _readCount);

    public FocusedTableRow this[int index]
    {
        get
        {
            Interlocked.Increment(ref _readCount);
            if (index == gateRowIndex)
            {
                GateReached.TrySetResult();
                _gate.Wait();
            }

            return rows[index];
        }
    }

    public IEnumerator<FocusedTableRow> GetEnumerator() => ((IReadOnlyList<FocusedTableRow>)rows).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        rows.GetEnumerator();

    public void ReleaseGate() => _gate.Set();

    public void Dispose() => _gate.Dispose();
}

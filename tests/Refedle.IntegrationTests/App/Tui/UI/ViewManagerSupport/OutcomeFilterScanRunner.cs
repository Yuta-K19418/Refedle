using Refedle.App.Tui.Workers;
using Refedle.Engine.Filtering;

namespace Refedle.IntegrationTests.App.Tui.UI.ViewManagerSupport;

/// <summary>
/// <see cref="IFilterScanRunner"/> double whose scans stay pending until the test completes,
/// faults or cancels the call with the given index.
/// </summary>
internal sealed class OutcomeFilterScanRunner : IFilterScanRunner
{
    private readonly List<TaskCompletionSource> _scans = [];

    public Task RunAsync(IFilterRowIndexer filterIndexer, CancellationToken ct)
    {
        TaskCompletionSource scan = new();
        _scans.Add(scan);
        return scan.Task;
    }

    public void Complete(int callIndex) => _scans[callIndex].SetResult();

    public void End(int callIndex, ScanOutcome outcome)
    {
        _ = outcome == ScanOutcome.Faulted
            ? _scans[callIndex].TrySetException(new InvalidOperationException("scan failed"))
            : _scans[callIndex].TrySetCanceled();
    }
}

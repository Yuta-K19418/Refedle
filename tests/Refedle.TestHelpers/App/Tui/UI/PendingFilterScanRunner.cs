using Refedle.App.Tui.Workers;
using Refedle.Engine.Filtering;

namespace Refedle.TestHelpers.App.Tui.UI;

/// <summary>
/// <see cref="IFilterScanRunner"/> double whose scans stay pending until the test ends
/// them, so completion handling is observed deterministically.
/// </summary>
internal sealed class PendingFilterScanRunner : IFilterScanRunner
{
    private readonly List<TaskCompletionSource> _scans = [];
    private readonly List<CancellationToken> _tokens = [];

    public List<CancellationToken> Tokens => _tokens;

    public Task RunAsync(IFilterRowIndexer filterIndexer, CancellationToken ct)
    {
        _tokens.Add(ct);
        TaskCompletionSource scan = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _scans.Add(scan);
        return scan.Task;
    }

    public void Complete(int callIndex) => _scans[callIndex].TrySetResult();

    public void Cancel(int callIndex) => _scans[callIndex].TrySetCanceled();

    public void End(int callIndex) =>
        _scans[callIndex].TrySetException(new InvalidOperationException("scan failed"));
}

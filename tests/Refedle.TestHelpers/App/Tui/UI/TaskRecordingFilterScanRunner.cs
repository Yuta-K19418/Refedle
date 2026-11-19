using Refedle.App.Tui.Workers;
using Refedle.Engine.Filtering;

namespace Refedle.TestHelpers.App.Tui.UI;

/// <summary>
/// <see cref="IFilterScanRunner"/> double that delegates to a real
/// <see cref="FilterIndexingRunner"/> while recording each started task in start order,
/// so a test can await a specific scan's task and observe how it terminated —
/// a deterministic replacement for read-count probing of the gated rows.
/// </summary>
internal sealed class TaskRecordingFilterScanRunner : IFilterScanRunner
{
    private sealed record Run(IFilterRowIndexer Indexer, Task Task);

    private readonly FilterIndexingRunner _inner = new();
    private readonly Lock _lock = new();
    private readonly List<Run> _runs = [];

    public Task RunAsync(IFilterRowIndexer filterIndexer, CancellationToken ct)
    {
        Task task = _inner.RunAsync(filterIndexer, ct);
        lock (_lock)
        {
            _runs.Add(new Run(filterIndexer, task));
        }

        return task;
    }

    /// <summary>The task of the scan started at <paramref name="index"/>, in start order.</summary>
    public Task TaskAt(int index)
    {
        lock (_lock)
        {
            return _runs[index].Task;
        }
    }

    /// <summary>Snapshot of every started scan task, in start order.</summary>
    public IReadOnlyList<Task> Tasks()
    {
        lock (_lock)
        {
            return [.. _runs.Select(static run => run.Task)];
        }
    }
}

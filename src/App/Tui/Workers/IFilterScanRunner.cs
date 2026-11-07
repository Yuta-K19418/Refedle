using Refedle.Engine.Filtering;

namespace Refedle.App.Tui.Workers;

/// <summary>
/// Runs a filter index build on a background thread. Implemented on the worker side
/// so the UI layer never calls <c>Task.Run</c> directly.
/// </summary>
internal interface IFilterScanRunner
{
    /// <summary>
    /// Runs <paramref name="filterIndexer"/>'s index build on a background thread.
    /// </summary>
    /// <param name="filterIndexer">The filter row indexer to build.</param>
    /// <param name="ct">The cancellation token to observe.</param>
    /// <returns>The task representing the background index build.</returns>
    Task RunAsync(IFilterRowIndexer filterIndexer, CancellationToken ct);
}

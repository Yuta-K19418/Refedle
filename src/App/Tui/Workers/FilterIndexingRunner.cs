using Refedle.Engine.Filtering;

namespace Refedle.App.Tui.Workers;

/// <summary>
/// Runs a filter index build on a background thread.
/// </summary>
internal static class FilterIndexingRunner
{
    /// <summary>
    /// Runs <paramref name="filterIndexer"/>'s index build on a background thread.
    /// </summary>
    /// <param name="filterIndexer">The filter row indexer to build.</param>
    /// <param name="ct">The cancellation token to observe.</param>
    /// <returns>The task representing the background index build.</returns>
    public static Task RunAsync(IFilterRowIndexer filterIndexer, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filterIndexer);
        return Task.Run(() => filterIndexer.BuildIndexAsync(ct), ct);
    }
}

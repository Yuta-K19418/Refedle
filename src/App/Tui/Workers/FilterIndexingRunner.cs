using Refedle.Engine.Filtering;

namespace Refedle.App.Tui.Workers;

/// <summary>
/// <see cref="IFilterScanRunner"/> that builds the index on a <c>Task.Run</c> thread.
/// </summary>
internal sealed class FilterIndexingRunner : IFilterScanRunner
{
    /// <inheritdoc/>
    public Task RunAsync(IFilterRowIndexer filterIndexer, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filterIndexer);
        return Task.Run(() => filterIndexer.BuildIndexAsync(ct), ct);
    }
}

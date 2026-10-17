using Refedle.Engine.IO;

namespace Refedle.App.Tui.Workers;

/// <summary>
/// Bridges the <see cref="IRowIndexer.BuildIndexCompleted"/> event to a <see cref="Task"/>.
/// </summary>
internal static class IndexingCompletion
{
    /// <summary>
    /// Creates a task that completes when <paramref name="indexer"/> finishes indexing,
    /// including when indexing has already finished. Cancellation and failure of indexing
    /// are not distinguished from success; the task completes normally in every case.
    /// </summary>
    /// <param name="indexer">The indexer to observe.</param>
    /// <returns>A <see cref="Task"/> that completes once indexing has finished.</returns>
    public static Task WhenCompletedAsync(IRowIndexer indexer)
    {
        ArgumentNullException.ThrowIfNull(indexer);

        var indexingCompleted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Subscribe first, then check, so completion is never missed.
        indexer.BuildIndexCompleted += () => indexingCompleted.TrySetResult();
        if (indexer.IsIndexingCompleted)
        {
            indexingCompleted.TrySetResult();
        }

        return indexingCompleted.Task;
    }
}

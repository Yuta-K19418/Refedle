using Refedle.Engine.Models;

namespace Refedle.App.Schema;

/// <summary>
/// Starts background schema refinement for a completed initial scan and publishes the
/// refined schema on the UI thread.
/// </summary>
internal static class BackgroundSchemaRefiner
{
    /// <summary>
    /// Starts the background refinement of <paramref name="initialSchema"/> and publishes the
    /// refined schema through <paramref name="uiThreadInvoke"/>.
    /// The returned task completes once the refined schema has been handed to <paramref name="uiThreadInvoke"/> or discarded.
    /// </summary>
    /// <param name="state">The application state to update with the refined schema.</param>
    /// <param name="scanner">The scanner that produced <paramref name="initialSchema"/>.</param>
    /// <param name="initialSchema">The schema produced by the initial scan.</param>
    /// <param name="uiThreadInvoke">Marshals the publish callback onto the UI thread.</param>
    /// <param name="cancellationToken">Token to stop the background scan.</param>
    /// <returns>A <see cref="Task"/> that completes once the refined schema has been handed to <paramref name="uiThreadInvoke"/> or discarded.</returns>
    public static Task StartAsync(
        AppState state,
        ISchemaScanner scanner,
        TableSchema initialSchema,
        Action<Action> uiThreadInvoke,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(initialSchema);
        ArgumentNullException.ThrowIfNull(uiThreadInvoke);

        return RunAsync();

        async Task RunAsync()
        {
            // WhenAny observes completion without propagating a faulted or canceled scan task.
            Task<TableSchema> scanTask = scanner.StartBackgroundScanAsync(initialSchema, cancellationToken);
            await Task.WhenAny(scanTask).ConfigureAwait(false);

            if (!scanTask.IsCompletedSuccessfully)
            {
                return;
            }

            TableSchema refinedSchema = await scanTask.ConfigureAwait(false);
            uiThreadInvoke(() =>
            {
                state.Schema = refinedSchema;
                state.OnSchemaRefined?.Invoke(refinedSchema);
            });
        }
    }
}

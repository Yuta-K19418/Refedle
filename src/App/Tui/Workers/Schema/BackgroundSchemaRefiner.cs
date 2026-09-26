using Refedle.Engine.Models;

namespace Refedle.App.Tui.Workers.Schema;

/// <summary>
/// Starts background schema refinement for a completed initial scan and publishes the
/// refined schema on the UI thread.
/// </summary>
/// <remarks>
/// <see cref="ISchemaScanner.StartBackgroundScanAsync"/> reports cancellation as
/// successful completion, so staleness is detected solely from the captured token:
/// <see cref="AppState.RenewCtsWithCancel"/> cancels it whenever a session is replaced.
/// The token is checked before posting and re-checked inside the UI callback, because
/// the session can be replaced between posting and execution.
/// </remarks>
internal static class BackgroundSchemaRefiner
{
    /// <summary>
    /// Starts the background refinement of <paramref name="initialSchema"/> and publishes the
    /// refined schema through <paramref name="uiThreadInvoke"/> while the session is still current.
    /// The returned task completes once the refined schema has been handed to <paramref name="uiThreadInvoke"/> or discarded.
    /// </summary>
    /// <param name="state">The application state to update with the refined schema.</param>
    /// <param name="scanner">The scanner that produced <paramref name="initialSchema"/>.</param>
    /// <param name="initialSchema">The schema produced by the initial scan.</param>
    /// <param name="uiThreadInvoke">Marshals the publish callback onto the UI thread.</param>
    /// <param name="scanToken">The token captured when the scans started.</param>
    /// <returns>A <see cref="Task"/> that completes once the refined schema has been handed to <paramref name="uiThreadInvoke"/> or discarded.</returns>
    public static Task StartAsync(
        AppState state,
        ISchemaScanner scanner,
        TableSchema initialSchema,
        Action<Action> uiThreadInvoke,
        CancellationToken scanToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(initialSchema);
        ArgumentNullException.ThrowIfNull(uiThreadInvoke);

        return RunAsync();

        async Task RunAsync()
        {
            // WhenAny observes completion without propagating a faulted or canceled scan task.
            Task<TableSchema> scanTask = scanner.StartBackgroundScanAsync(initialSchema, scanToken);
            await Task.WhenAny(scanTask).ConfigureAwait(false);

            // Skip posting for an already-cancelled scan; the callback re-checks the
            // token because the session can be replaced between posting and execution.
            if (!scanTask.IsCompletedSuccessfully || scanToken.IsCancellationRequested)
            {
                return;
            }

            TableSchema refinedSchema = await scanTask.ConfigureAwait(false);
            uiThreadInvoke(() =>
            {
                // Re-check the token on the UI thread: the session can be replaced after posting.
                if (scanToken.IsCancellationRequested)
                {
                    return;
                }

                state.Schema = refinedSchema;
                state.OnSchemaRefined?.Invoke(refinedSchema);
            });
        }
    }
}

using Refedle.Engine.Models;

namespace Refedle.App.Schema;

/// <summary>
/// Starts background schema refinement for a completed initial scan and publishes the
/// refined schema on the UI thread.
/// </summary>
/// <remarks>
/// <see cref="IncrementalSchemaScannerBase.StartBackgroundScanAsync"/> reports cancellation as
/// successful completion, so before posting only the thread-safe captured token is checked.
/// The full session validation (token and <c>CurrentFilePath</c>) runs inside the UI callback,
/// the only place UI-owned state is read, keeping a replaced file's state untouched.
/// </remarks>
internal static class BackgroundSchemaRefiner
{
    /// <summary>
    /// Starts the background refinement of <paramref name="initialSchema"/> and publishes the
    /// refined schema through <paramref name="uiThreadInvoke"/> while the session is still current.
    /// Returns the fire-and-forget continuation so callers (and tests) can observe its completion.
    /// </summary>
    /// <param name="state">The application state to update with the refined schema.</param>
    /// <param name="scanner">The scanner that produced <paramref name="initialSchema"/>.</param>
    /// <param name="initialSchema">The schema produced by the initial scan.</param>
    /// <param name="scannedFilePath">The file path captured when the scans started.</param>
    /// <param name="uiThreadInvoke">Marshals the publish callback onto the UI thread.</param>
    /// <param name="scanToken">The token captured when the scans started.</param>
    /// <returns>A <see cref="Task"/> representing the fire-and-forget continuation.</returns>
    public static Task StartAsync(
        AppState state,
        IncrementalSchemaScannerBase scanner,
        TableSchema initialSchema,
        string scannedFilePath,
        Action<Action> uiThreadInvoke,
        CancellationToken scanToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(initialSchema);
        ArgumentNullException.ThrowIfNull(uiThreadInvoke);

        return scanner
            .StartBackgroundScanAsync(initialSchema, scanToken)
            .ContinueWith(
                t =>
                {
                    // Token-only check off the UI thread: CurrentFilePath is owned by the UI
                    // thread; the authoritative path re-check runs inside the UI callback.
                    if (!t.IsCompletedSuccessfully || scanToken.IsCancellationRequested)
                    {
                        return;
                    }

                    uiThreadInvoke(() =>
                    {
                        if (state.IsStaleSession(scannedFilePath, scanToken))
                        {
                            return;
                        }

                        state.Schema = t.Result;
                        state.OnSchemaRefined?.Invoke(t.Result);
                    });
                },
                TaskScheduler.Default
            );
    }
}

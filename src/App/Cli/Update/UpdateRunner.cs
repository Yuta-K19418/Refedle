namespace Refedle.App.Cli.Update;

/// <summary>
/// Composition root for the <c>refedle update</c> command: wires the production dependencies
/// and runs <see cref="UpdateCommand"/>.
/// </summary>
internal static class UpdateRunner
{
    /// <summary>
    /// Runs the self-update flow with the production dependencies.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exit code: <see cref="ExitCode.Success"/> on success, <see cref="ExitCode.Failure"/> on any failure.</returns>
    public static async ValueTask<ExitCode> RunAsync(CancellationToken ct)
    {
        using var releaseClient = new GitHubReleaseClient();
        var command = new UpdateCommand(
            BuildInfo.Version,
            releaseClient,
            new ArchiveBinaryReplacer(),
            new RuntimeIdentifierResolver(),
            new ConsoleAppLogger());
        return await command.RunAsync(ct).ConfigureAwait(false);
    }
}

namespace Refedle.App.Cli.Commands.Update;

/// <summary>
/// Runs <c>refedle update</c> with the caller-supplied logger and status reporter, creating
/// the remaining production update dependencies and running <see cref="UpdateCommand"/>.
/// </summary>
internal static class UpdateRunner
{
    /// <summary>
    /// Runs the self-update flow with the caller-supplied logger and status reporter,
    /// creating the remaining production update dependencies.
    /// </summary>
    /// <param name="logger">The app logger for logging messages.</param>
    /// <param name="statusReporter">Shows the current phase while updating.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exit code: <see cref="ExitCode.Success"/> on success, <see cref="ExitCode.Failure"/> on any failure.</returns>
    public static async ValueTask<ExitCode> RunAsync(IAppLogger logger, IStatusReporter statusReporter, CancellationToken ct)
    {
        using var releaseClient = new GitHubReleaseClient();
        var command = new UpdateCommand(
            BuildInfo.Version,
            releaseClient,
            new ArchiveBinaryReplacer(),
            new RuntimeIdentifierResolver(),
            logger,
            statusReporter);
        return await command.RunAsync(ct).ConfigureAwait(false);
    }
}

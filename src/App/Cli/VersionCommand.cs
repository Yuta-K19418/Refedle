namespace Refedle.App.Cli;

/// <summary>
/// The <c>refedle version</c> / <c>--version</c> command: prints the running build version.
/// </summary>
/// <param name="version">The version of the running binary, e.g. <see cref="BuildInfo.Version"/>.</param>
/// <param name="logger">The logger used for output.</param>
internal sealed class VersionCommand(string version, IAppLogger logger)
{
    /// <summary>
    /// Decides whether the given command-line arguments should print the version. <c>--version</c>
    /// wins from any position; the <c>version</c> subcommand is only recognized in first position.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns><c>true</c> when the version output should be produced.</returns>
    public static bool IsMatch(string[] args)
        => args.Contains("--version") || args is ["version", ..];

    /// <summary>
    /// Writes the version line and returns success.
    /// </summary>
    /// <returns><see cref="ExitCode.Success"/>.</returns>
    public async Task<ExitCode> RunAsync()
    {
        await logger.WriteInfoAsync($"refedle {version}").ConfigureAwait(false);
        return ExitCode.Success;
    }
}

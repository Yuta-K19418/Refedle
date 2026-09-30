using System.Diagnostics;
using Refedle.App.Cli.Commands.Apply;
using Refedle.App.Cli.Commands.Help;
using Refedle.App.Cli.Commands.Update;
using Refedle.App.Cli.Commands.Version;

namespace Refedle.App.Cli;

/// <summary>
/// Runs the headless CLI command previously identified by <see cref="CliCommandMatcher"/>,
/// using the supplied logger and status reporter.
/// </summary>
internal static class CliDispatcher
{
    private static readonly ConsoleCancelKeyPressSource _cancelKeyPressSource = new();

    /// <summary>
    /// Runs the given <see cref="CliCommand"/> using the supplied logger and status reporter.
    /// </summary>
    /// <param name="command">The command to run.</param>
    /// <param name="args">The raw command-line arguments (including the subcommand token, if any).</param>
    /// <param name="logger">The app logger for logging messages.</param>
    /// <param name="statusReporter">Shows the current phase while a command is running.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The exit code for the process.</returns>
    public static ValueTask<ExitCode> RunAsync(
        CliCommand command,
        string[] args,
        IAppLogger logger,
        IStatusReporter statusReporter,
        CancellationToken ct) =>
        command switch
        {
            CliCommand.Help => RunHelpAsync(logger),
            CliCommand.Version => RunVersionAsync(logger),
            CliCommand.Update => RunUpdateAsync(logger, statusReporter, ct),
            CliCommand.Apply => RunApplyAsync(args, logger, statusReporter, ct),
            _ => throw new UnreachableException(),
        };

    private static async ValueTask<ExitCode> RunHelpAsync(IAppLogger logger) =>
        await new HelpCommand(logger).RunAsync().ConfigureAwait(false);

    private static async ValueTask<ExitCode> RunVersionAsync(IAppLogger logger) =>
        await new VersionCommand(BuildInfo.Version, logger).RunAsync().ConfigureAwait(false);

    private static async ValueTask<ExitCode> RunUpdateAsync(IAppLogger logger, IStatusReporter statusReporter, CancellationToken ct)
    {
        using var scope = CliCancellationScope.Create(_cancelKeyPressSource, ct);
        return await UpdateRunner.RunAsync(logger, statusReporter, scope.Token).ConfigureAwait(false);
    }

    private static async ValueTask<ExitCode> RunApplyAsync(string[] args, IAppLogger logger, IStatusReporter statusReporter, CancellationToken ct)
    {
        // The matcher only yields CliCommand.Apply for args beginning with "apply"; reaching
        // this method without the token is a programming error, not a user-input condition.
        if (args is not ["apply", .. var applyArgs])
        {
            throw new ArgumentException(
                "CliDispatcher received CliCommand.Apply but args does not begin with the \"apply\" token.",
                nameof(args));
        }

        using var scope = CliCancellationScope.Create(_cancelKeyPressSource, ct);
        return await ApplyRunner.RunAsync(applyArgs, logger, statusReporter, scope.Token).ConfigureAwait(false);
    }
}

using System.Diagnostics;
using Refedle.App.Cli.Commands.Apply;
using Refedle.App.Cli.Commands.Help;
using Refedle.App.Cli.Commands.Update;
using Refedle.App.Cli.Commands.Version;

namespace Refedle.App.Cli;

/// <summary>
/// Runs the headless CLI command previously identified by <see cref="CliCommandMatcher"/>,
/// wiring each command to its production dependencies.
/// </summary>
internal static class CliDispatcher
{
    private static readonly ConsoleCancelKeyPressSource _cancelKeyPressSource = new();

    /// <summary>
    /// Runs the given <see cref="CliCommand"/> with the production dependencies.
    /// </summary>
    /// <param name="command">The command to run.</param>
    /// <param name="args">The raw command-line arguments (including the subcommand token, if any).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The exit code for the process.</returns>
    public static ValueTask<ExitCode> RunAsync(CliCommand command, string[] args, CancellationToken ct) =>
        command switch
        {
            CliCommand.Help => RunHelpAsync(),
            CliCommand.Version => RunVersionAsync(),
            CliCommand.Update => RunUpdateAsync(ct),
            CliCommand.Apply => RunApplyAsync(args, ct),
            _ => throw new UnreachableException(),
        };

    private static async ValueTask<ExitCode> RunHelpAsync() =>
        await new HelpCommand(new ConsoleAppLogger()).RunAsync().ConfigureAwait(false);

    private static async ValueTask<ExitCode> RunVersionAsync() =>
        await new VersionCommand(BuildInfo.Version, new ConsoleAppLogger()).RunAsync().ConfigureAwait(false);

    private static async ValueTask<ExitCode> RunUpdateAsync(CancellationToken ct)
    {
        using var scope = CliCancellationScope.Create(_cancelKeyPressSource, ct);
        return await UpdateRunner.RunAsync(scope.Token).ConfigureAwait(false);
    }

    private static async ValueTask<ExitCode> RunApplyAsync(string[] args, CancellationToken ct)
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
        return await ApplyRunner.RunAsync(applyArgs, scope.Token).ConfigureAwait(false);
    }
}

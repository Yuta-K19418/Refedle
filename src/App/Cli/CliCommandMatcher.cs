using Refedle.App.Cli.Commands.Help;
using Refedle.App.Cli.Commands.Version;

namespace Refedle.App.Cli;

/// <summary>
/// Decides whether the raw command-line arguments select a headless CLI command, and if so, which one.
/// </summary>
internal static class CliCommandMatcher
{
    /// <summary>
    /// Attempts to match the given command-line arguments to a <see cref="CliCommand"/>.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <param name="command">The matched command when this method returns <c>true</c>; otherwise undefined.</param>
    /// <returns><c>true</c> when the arguments select a CLI command; <c>false</c> when the TUI should launch instead.</returns>
    public static bool TryMatch(string[] args, out CliCommand command)
    {
        // "--help" / "-h" win even when combined with other modes (e.g. "refedle apply --help") and take
        // precedence over "--version", while the "help" subcommand form is only recognized as the first
        // argument so that a positional file name never triggers the help output.
        if (HelpCommand.IsMatch(args))
        {
            command = CliCommand.Help;
            return true;
        }

        // "--version" wins even when combined with other modes (e.g. "refedle apply --version"),
        // while the "version" subcommand form is only recognized as the first argument so that a
        // positional file name never triggers the version output.
        if (VersionCommand.IsMatch(args))
        {
            command = CliCommand.Version;
            return true;
        }

        if (args is ["update", ..])
        {
            command = CliCommand.Update;
            return true;
        }

        if (args is ["apply", ..])
        {
            command = CliCommand.Apply;
            return true;
        }

        command = default;
        return false;
    }
}

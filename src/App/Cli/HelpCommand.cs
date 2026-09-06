namespace Refedle.App.Cli;

/// <summary>
/// The <c>refedle help</c> / <c>--help</c> / <c>-h</c> command: prints the top-level help text.
/// </summary>
/// <param name="logger">The logger used for output.</param>
internal sealed class HelpCommand(IAppLogger logger)
{
    private const string HelpText =
        """
        refedle - interactive TUI and headless CLI for reshaping CSV / JSON / JSON Lines data

        Usage:
          refedle [--file <path>] [--recipe <path>]   Launch the interactive TUI
          refedle <command> [options]

        Commands:
          apply      Apply a recipe to an input file and write the result (headless)
          update     Download and install the latest refedle release
          version    Print the version
          help       Show this help

        apply options:
          --input <path>     Input data file (CSV / JSON / JSON Lines)
          --recipe <path>    Recipe YAML to apply
          --output <path>    Output file (optional with --dry-run)
          --dry-run          Validate and print the resolved plan without writing output

        Options:
          --file <path>      Open the given data file on TUI startup
          --recipe <path>    Load the given recipe on TUI startup
          --version          Print the version
          --help, -h         Show this help
        """;

    /// <summary>
    /// Decides whether the given command-line arguments should print the help. <c>--help</c> /
    /// <c>-h</c> win from any position; the <c>help</c> subcommand is only recognized in first position.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns><c>true</c> when the help output should be produced.</returns>
    public static bool IsMatch(string[] args)
        => args.Contains("--help") || args.Contains("-h") || args is ["help", ..];

    /// <summary>
    /// Writes the help text and returns success.
    /// </summary>
    /// <returns><see cref="ExitCode.Success"/>.</returns>
    public async Task<ExitCode> RunAsync()
    {
        await logger.WriteInfoAsync(HelpText).ConfigureAwait(false);
        return ExitCode.Success;
    }
}

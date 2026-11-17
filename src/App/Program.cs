using Refedle.App.Cli;
using Refedle.App.Tui.UI;

if (CliCommandMatcher.TryMatch(args, out CliCommand cliCommand))
{
    ConsoleAppLogger logger = new();
    SpectreStatusReporter statusReporter = new();
    return (int)await CliDispatcher.RunAsync(cliCommand, args, logger, statusReporter, CancellationToken.None).ConfigureAwait(false);
}

return (int)await TuiDispatcher.RunAsync(args, CancellationToken.None).ConfigureAwait(false);

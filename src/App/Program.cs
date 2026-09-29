using Refedle.App;
using Refedle.App.Cli;

if (CliCommandMatcher.TryMatch(args, out var cliCommand))
{
    var logger = new ConsoleAppLogger();
    var statusReporter = new SpectreStatusReporter();
    return (int)await CliDispatcher.RunAsync(cliCommand, args, logger, statusReporter, CancellationToken.None).ConfigureAwait(false);
}

return (int)await TuiDispatcher.RunAsync(args, CancellationToken.None).ConfigureAwait(false);

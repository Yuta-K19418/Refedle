using Refedle.App.Cli;
using Refedle.App.Tui.UI;

if (CliCommandMatcher.TryMatch(args, out var cliCommand))
{
    return (int)await CliDispatcher.RunAsync(cliCommand, args, CancellationToken.None).ConfigureAwait(false);
}

return (int)await TuiDispatcher.RunAsync(args, CancellationToken.None).ConfigureAwait(false);

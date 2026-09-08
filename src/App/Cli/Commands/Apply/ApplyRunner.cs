using Refedle.App.Cli.Parsing;

namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// Composition root for the <c>refedle apply</c> command: parses the batch-conversion
/// arguments and runs <see cref="Runner"/> — or <see cref="DryRunner"/> for a dry run —
/// with the production dependencies.
/// </summary>
internal static class ApplyRunner
{
    /// <summary>
    /// Parses the arguments following the <c>apply</c> subcommand and runs the CLI headless
    /// batch processing pipeline with the production dependencies. With <c>--dry-run</c>,
    /// validates and reports the resolved plan without writing any output.
    /// </summary>
    /// <param name="args">The arguments following the <c>apply</c> token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exit code: <see cref="ExitCode.Success"/> on success, <see cref="ExitCode.Failure"/> on any failure.</returns>
    public static async ValueTask<ExitCode> RunAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var parseResult = ArgumentParser.Parse(args);
        if (parseResult.IsFailure)
        {
            await Console.Error.WriteLineAsync(parseResult.Error).ConfigureAwait(false);
            return ExitCode.Failure;
        }

        var parsedArgs = parseResult.Value;
        var logger = new ConsoleAppLogger();
        if (parsedArgs.IsDryRun)
        {
            return await DryRunner.RunAsync(parsedArgs, logger, ct).ConfigureAwait(false);
        }

        return await Runner.RunAsync(parsedArgs, logger, ct).ConfigureAwait(false);
    }
}

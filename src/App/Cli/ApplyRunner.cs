namespace Refedle.App.Cli;

/// <summary>
/// Composition root for the <c>refedle apply</c> command: parses the batch-conversion
/// arguments and runs <see cref="Runner"/> with the production dependencies.
/// </summary>
internal static class ApplyRunner
{
    /// <summary>
    /// Parses the arguments following the <c>apply</c> subcommand and runs the CLI headless
    /// batch processing pipeline with the production dependencies.
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

        return await Runner.RunAsync(parseResult.Value, new ConsoleAppLogger(), ct).ConfigureAwait(false);
    }
}

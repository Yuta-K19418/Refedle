using System.Diagnostics;
using Refedle.Engine.Types;

namespace Refedle.App.Cli;

/// <summary>
/// Orchestrates CLI headless batch processing pipeline:
/// preparation (recipe load → column resolution → output schema build) → transform → write.
/// Supports CSV and JSON Lines for both input and output (cross-format conversion included).
/// </summary>
internal static class Runner
{
    /// <summary>
    /// Runs CLI headless batch processing pipeline.
    /// </summary>
    /// <param name="args">The validated CLI arguments.</param>
    /// <param name="logger">The app logger for logging messages.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exit code: <see cref="ExitCode.Success"/> on success, <see cref="ExitCode.Failure"/> on any failure.</returns>
    public static async ValueTask<ExitCode> RunAsync(Arguments args, IAppLogger logger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        // The parser already rejects a missing --output for a normal run; this guards direct
        // RunAsync callers against the null that is only valid for a dry run.
        if (string.IsNullOrWhiteSpace(args.OutputFile))
        {
            await logger.WriteErrorAsync("Missing required flag: --output");
            return ExitCode.Failure;
        }

        try
        {
            var preparationResult = await ApplyPreparer.PrepareAsync(
                args.InputFile, args.RecipeFile, args.OutputFile, logger, ct).ConfigureAwait(false);
            if (preparationResult.IsFailure)
            {
                return ExitCode.Failure;
            }

            var preparation = preparationResult.Value;
            if (preparation.OutputFormat is not DataFormat outputFormat)
            {
                // Unreachable: an output file is given on this path, so PrepareAsync resolved its format.
                throw new UnreachableException("Output format was not resolved for the write path.");
            }

            // Dispatch to generated static monomorphization logic
            return await Generated.FormatDispatcher.DispatchAsync(
                preparation.InputFormat, outputFormat, args.InputFile, args.OutputFile,
                preparation.Recipe.DrillDownKeyPath, preparation.ColumnNames, preparation.OutputSchema, logger, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await logger.WriteErrorAsync("Operation cancelled");
            return ExitCode.Failure;
        }
        catch (NotSupportedException ex)
        {
            await logger.WriteErrorAsync(ex.Message);
            return ExitCode.Failure;
        }
        catch (Exception ex)
        {
            await logger.WriteErrorAsync($"Error: {ex.Message}");
            return ExitCode.Failure;
        }
    }
}

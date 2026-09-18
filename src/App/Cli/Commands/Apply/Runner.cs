using System.Diagnostics;
using Refedle.App.Cli.Parsing;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// Orchestrates CLI headless batch processing pipeline:
/// preparation (recipe load → column resolution → output schema build) → transform → write.
/// Supports CSV and JSON Lines for both input and output (cross-format conversion included).
/// </summary>
internal static class Runner
{
    /// <summary>
    /// Runs CLI headless batch processing pipeline. Output is written to a temp file in the
    /// same directory as the real output path and renamed into place only on success, so a
    /// mid-run failure never leaves a truncated file at the real output path. A symlinked
    /// <c>--output</c> is resolved to its final target before publishing, so content lands at
    /// the link's destination, matching the pre-atomic-write <c>FileMode.Create</c> behavior.
    /// Cell issues collected during a successful run are reported as warnings once, after the
    /// output has been published.
    /// </summary>
    /// <param name="args">The validated CLI arguments.</param>
    /// <param name="logger">The app logger for logging messages.</param>
    /// <param name="dispatcher">The format dispatcher that runs the batch pipeline.</param>
    /// <param name="tempOutputPathProvider">Mints temp paths for the atomic output write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exit code: <see cref="ExitCode.Success"/> on success, <see cref="ExitCode.Failure"/> on any failure.</returns>
    public static async ValueTask<ExitCode> RunAsync(
        Arguments args,
        IAppLogger logger,
        IFormatDispatcher dispatcher,
        ITempOutputPathProvider tempOutputPathProvider,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        // The parser already rejects a missing --output for a normal run; this guards direct
        // RunAsync callers against the null that is only valid for a dry run.
        if (string.IsNullOrWhiteSpace(args.OutputFile))
        {
            await logger.WriteErrorAsync("Missing required flag: --output").ConfigureAwait(false);
            return ExitCode.Failure;
        }

        // Null until PrepareAsync succeeds: a preparation failure means no temp path was ever computed.
        string? tempOutputFile = null;
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

            // File.Move replaces a destination symlink itself instead of following it, so
            // resolve the final target first to keep publishing through the link. LinkTarget
            // detects broken links without following them; null means a fresh output path,
            // which ResolveLinkTarget cannot resolve (it throws on a missing path).
            var outputInfo = new FileInfo(args.OutputFile);
            var publishPath = outputInfo.LinkTarget is null
                ? args.OutputFile
                : outputInfo.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? args.OutputFile;

            // The publish path is only ever touched by the rename below; the batch itself
            // writes to the temp path. Unconditional overwrite matches FileMode.Create semantics.
            tempOutputFile = tempOutputPathProvider.NewPath(publishPath);
            var runResult = await dispatcher.DispatchAsync(
                preparation.InputFormat, outputFormat, args.InputFile, tempOutputFile,
                preparation.Recipe.DrillDownKeyPath, preparation.ColumnNames, preparation.OutputSchema, logger, ct).ConfigureAwait(false);
            if (runResult.ExitCode is ExitCode.Success)
            {
                File.Move(tempOutputFile, publishPath, overwrite: true);

                // Reported only on success: a failed run discards its output and already
                // reports its own error, so cell-level detail would only add noise.
                await CellIssueReporter.ReportAsync(
                    runResult.CellIssues, runResult.HasMoreCellIssues, logger).ConfigureAwait(false);
            }

            return runResult.ExitCode;
        }
        catch (OperationCanceledException)
        {
            await logger.WriteErrorAsync("Operation cancelled").ConfigureAwait(false);
            return ExitCode.Failure;
        }
        catch (NotSupportedException ex)
        {
            await logger.WriteErrorAsync(ex.Message).ConfigureAwait(false);
            return ExitCode.Failure;
        }
        catch (Exception ex)
        {
            await logger.WriteErrorAsync($"Error: {ex.Message}").ConfigureAwait(false);
            return ExitCode.Failure;
        }
        finally
        {
            if (tempOutputFile is not null && File.Exists(tempOutputFile))
            {
                // Best-effort cleanup: on success this is a no-op (already moved away).
                try
                {
                    File.Delete(tempOutputFile);
                }
                catch (Exception)
                {
                    // Swallowed intentionally: cleanup must not mask the already-decided exit code.
                }
            }
        }
    }
}

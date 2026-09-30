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
    /// <param name="statusReporter">Shows the current phase while running; the indicator shown while preparing and converting.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exit code: <see cref="ExitCode.Success"/> on success, <see cref="ExitCode.Failure"/> on any failure.</returns>
    public static async ValueTask<ExitCode> RunAsync(
        Arguments args,
        IAppLogger logger,
        IFormatDispatcher dispatcher,
        ITempOutputPathProvider tempOutputPathProvider,
        IStatusReporter statusReporter,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(statusReporter);

        // The parser already rejects a missing --output for a normal run; this guards direct
        // RunAsync callers against the null that is only valid for a dry run.
        if (string.IsNullOrWhiteSpace(args.OutputFile))
        {
            await logger.WriteErrorAsync("Missing required flag: --output").ConfigureAwait(false);
            return ExitCode.Failure;
        }

        var deferredLogger = new DeferredAppLogger();
        string outputFile = args.OutputFile;

        (BatchRunResult batchRunResult, string publishPath, string tempOutputFile)? appliedResult = null;
        try
        {
            try
            {
                appliedResult = await statusReporter.RunAsync<(BatchRunResult batchRunResult, string publishPath, string tempOutputFile)?>(
                    "Preparing...",
                    setPhase => ApplyAsync(
                        args, outputFile, deferredLogger, dispatcher, tempOutputPathProvider, setPhase, ct)).ConfigureAwait(false);
            }
            finally
            {
                // Messages are held while the spinner owns the terminal, then replayed in order.
                await deferredLogger.FlushToAsync(logger).ConfigureAwait(false);
            }

            if (appliedResult is null)
            {
                return ExitCode.Failure;
            }

            var (batchRunResult, publishPath, tempOutputFile) = appliedResult.Value;
            if (batchRunResult.ExitCode is ExitCode.Success)
            {
                await PublishAsync(batchRunResult, publishPath, tempOutputFile, logger).ConfigureAwait(false);
            }

            return batchRunResult.ExitCode;
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
            // Covers a throw between dispatch and publish (e.g. replaying deferred messages); no-op once moved.
            if (appliedResult is not null)
            {
                TryDeleteTempFile(appliedResult.Value.tempOutputFile);
            }
        }
    }

    private static async ValueTask PublishAsync(
        BatchRunResult batchRunResult,
        string publishPath,
        string tempOutputFile,
        IAppLogger logger)
    {
        try
        {
            File.Move(tempOutputFile, publishPath, overwrite: true);

            // Reported only on success: a failed run discards its output and already
            // reports its own error, so cell-level detail would only add noise.
            await CellIssueReporter.ReportAsync(
                batchRunResult.CellIssues, batchRunResult.HasMoreCellIssues, logger).ConfigureAwait(false);
        }
        finally
        {
            // No-op once the move succeeded; removes the temp file if the move itself failed.
            TryDeleteTempFile(tempOutputFile);
        }
    }

    // Returns null when preparation failed (already reported through the logger).
    private static async ValueTask<(BatchRunResult batchRunResult, string publishPath, string tempOutputFile)?> ApplyAsync(
        Arguments args,
        string outputFile,
        IAppLogger logger,
        IFormatDispatcher dispatcher,
        ITempOutputPathProvider tempOutputPathProvider,
        Action<string> setPhase,
        CancellationToken ct)
    {
        var preparationResult = await ApplyPreparer.PrepareAsync(
            args.InputFile, args.RecipeFile, outputFile, logger, ct).ConfigureAwait(false);
        if (preparationResult.IsFailure)
        {
            return null;
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
        var outputInfo = new FileInfo(outputFile);
        var publishPath = outputInfo.LinkTarget is null
            ? outputFile
            : outputInfo.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? outputFile;

        // The publish path is only ever touched by the rename after the batch; the batch itself
        // writes to the temp path. Unconditional overwrite matches FileMode.Create semantics.
        var tempOutputFile = tempOutputPathProvider.NewPath(publishPath);

        return await RunBatchAsync(
            preparation, outputFormat, publishPath, tempOutputFile, args, dispatcher, logger, setPhase, ct).ConfigureAwait(false);
    }

    private static async ValueTask<(BatchRunResult batchRunResult, string publishPath, string tempOutputFile)> RunBatchAsync(
        ApplyPreparation preparation,
        DataFormat outputFormat,
        string publishPath,
        string tempOutputFile,
        Arguments args,
        IFormatDispatcher dispatcher,
        IAppLogger logger,
        Action<string> setPhase,
        CancellationToken ct)
    {
        setPhase("Converting...");
        var keepTempFile = false;
        try
        {
            var batchRunResult = await dispatcher.DispatchAsync(
                preparation.InputFormat, outputFormat, args.InputFile, tempOutputFile,
                preparation.Recipe.DrillDownKeyPath, preparation.ColumnNames, preparation.OutputSchema, logger, ct).ConfigureAwait(false);

            // Only a successful batch hands its temp file on to be published.
            keepTempFile = batchRunResult.ExitCode is ExitCode.Success;
            return (batchRunResult, publishPath, tempOutputFile);
        }
        finally
        {
            if (!keepTempFile)
            {
                TryDeleteTempFile(tempOutputFile);
            }
        }
    }

    private static void TryDeleteTempFile(string tempOutputFile)
    {
        if (!File.Exists(tempOutputFile))
        {
            return;
        }

        try
        {
            File.Delete(tempOutputFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Swallowed intentionally: cleanup must not mask the already-decided exit code.
        }
    }
}

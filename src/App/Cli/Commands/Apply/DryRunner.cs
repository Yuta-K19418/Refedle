using System.Collections.Frozen;
using System.Diagnostics;
using Refedle.App.Cli.Parsing;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;

namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// Orchestrates the CLI dry-run pipeline: runs every preparation step of
/// <see cref="Runner"/> (recipe load → format detection → validation → column resolution →
/// output schema build) without transforming or writing any data, then prints a summary of
/// the resolved plan to stdout.
/// </summary>
internal static class DryRunner
{
    /// <summary>
    /// Runs the CLI dry-run pipeline and prints the resolved plan summary.
    /// </summary>
    /// <param name="args">The validated CLI arguments.</param>
    /// <param name="logger">The app logger for logging messages.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exit code: <see cref="ExitCode.Success"/> on success, <see cref="ExitCode.Failure"/> on any failure.</returns>
    public static async ValueTask<ExitCode> RunAsync(Arguments args, IAppLogger logger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        try
        {
            var preparationResult = await ApplyPreparer.PrepareAsync(
                args.InputFile, args.RecipeFile, args.OutputFile, logger, ct).ConfigureAwait(false);
            if (preparationResult.IsFailure)
            {
                return ExitCode.Failure;
            }

            await WriteSummaryAsync(preparationResult.Value, logger).ConfigureAwait(false);
            return ExitCode.Success;
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

    private static async ValueTask WriteSummaryAsync(ApplyPreparation preparation, IAppLogger logger)
    {
        await logger.WriteInfoAsync("Dry run OK").ConfigureAwait(false);
        await logger.WriteInfoAsync($"  Input format: {preparation.InputFormat}").ConfigureAwait(false);

        if (preparation.OutputFormat is { } outputFormat)
        {
            await logger.WriteInfoAsync($"  Output format: {outputFormat}").ConfigureAwait(false);
        }

        await logger.WriteInfoAsync(
            $"  Drill-down key path: {DescribeDrillDownKeyPath(preparation.Recipe.DrillDownKeyPath)}").ConfigureAwait(false);
        await logger.WriteInfoAsync(
            $"  Resolved input columns ({preparation.ColumnNames.Count}): {string.Join(", ", preparation.ColumnNames)}").ConfigureAwait(false);

        await logger.WriteInfoAsync("  Output schema:").ConfigureAwait(false);
        foreach (var column in preparation.OutputSchema.Columns)
        {
            await logger.WriteInfoAsync(
                $"    {column.SourceName} -> {column.OutputName}{DescribeTransform(column.Transform)}").ConfigureAwait(false);
        }

        if (preparation.OutputSchema.Filters.Count == 0)
        {
            return;
        }

        await logger.WriteInfoAsync("  Filters:").ConfigureAwait(false);
        foreach (var filter in preparation.OutputSchema.Filters)
        {
            await logger.WriteInfoAsync(
                $"    {preparation.ColumnNames[filter.SourceColumnIndex]} {_operatorSymbols[filter.Operator]} {filter.Value}").ConfigureAwait(false);
        }
    }

    private static string DescribeDrillDownKeyPath(IReadOnlyList<KeyPathSegment>? keyPath) =>
        keyPath is { Count: > 0 } ? KeyPathFormatter.Format(keyPath, collapseIndices: false) : "(none)";

    private static string DescribeTransform(CellTransformSpec? transform) =>
        transform switch
        {
            null => string.Empty,
            FillSpec fill => $"  [fill: {fill.Value}]",
            TimestampFormatSpec format => $"  [timestamp format: {format.TargetFormat}]",
            _ => throw new UnreachableException($"Unhandled transform type: {transform.GetType().Name}"),
        };

    // Symbols follow the FilterOperator XML docs; the text-match operators have no symbol form.
    private static readonly FrozenDictionary<FilterOperator, string> _operatorSymbols =
        new Dictionary<FilterOperator, string>
        {
            [FilterOperator.Equals] = "==",
            [FilterOperator.NotEquals] = "!=",
            [FilterOperator.GreaterThan] = ">",
            [FilterOperator.LessThan] = "<",
            [FilterOperator.GreaterThanOrEqual] = ">=",
            [FilterOperator.LessThanOrEqual] = "<=",
            [FilterOperator.Contains] = "contains",
            [FilterOperator.NotContains] = "not contains",
            [FilterOperator.StartsWith] = "starts with",
            [FilterOperator.EndsWith] = "ends with",
        }.ToFrozenDictionary();
}

using Refedle.Engine;
using Refedle.Engine.Recipes;
using Refedle.Engine.Types;

namespace Refedle.App.Cli;

/// <summary>
/// Orchestrates CLI headless batch processing pipeline:
/// recipe load → column resolution → output schema build → transform → write.
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

        try
        {
            // Load recipe
            var recipeResult = await new RecipeManager().LoadAsync(args.RecipeFile, ct).ConfigureAwait(false);
            if (recipeResult.IsFailure)
            {
                await logger.WriteErrorAsync($"Error loading recipe: {recipeResult.Error}");
                return ExitCode.Failure;
            }

            var recipe = recipeResult.Value;

            // Detect formats (throws NotSupportedException if invalid)
            var inputFormat = DetectFileFormat(args.InputFile);
            var outputFormat = DetectFileFormat(args.OutputFile);

            // Resolve the full input column name set (no type inference);
            // drillDownKeyPath is not yet sourced from the recipe
            var columnNames = ColumnNameResolver.ResolveColumnNames(
                inputFormat, args.InputFile, drillDownKeyPath: null, ct);

            // Build output schema
            var outputSchemaResult = ActionApplier.BuildOutputSchema(columnNames, recipe.Actions);
            if (outputSchemaResult.IsFailure)
            {
                await logger.WriteErrorAsync($"Error building output schema: {outputSchemaResult.Error}");
                return ExitCode.Failure;
            }

            var outputSchema = outputSchemaResult.Value;

            // Dispatch to generated static monomorphization logic
            return await Generated.FormatDispatcher.DispatchAsync(
                inputFormat, outputFormat, args.InputFile, args.OutputFile,
                drillDownKeyPath: null, columnNames, outputSchema, logger, ct).ConfigureAwait(false);
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

    private static DataFormat DetectFileFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToUpperInvariant();
        return extension switch
        {
            ".CSV" => DataFormat.Csv,
            ".JSONL" => DataFormat.JsonLines,
            // .json is a JSON array/object format, not JSON Lines — unsupported
            ".JSON" => throw new NotSupportedException($"Unsupported format: {extension} (Standard JSON format is not supported for batch processing. Use .jsonl for JSON Lines.)"),
            _ => throw new NotSupportedException($"Unsupported file extension: {extension}"),
        };
    }
}

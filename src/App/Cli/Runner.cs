using Refedle.Engine;
using Refedle.Engine.Recipes;

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

            // Detect formats: input by content, output by extension (it does not exist yet)
            var inputFormatResult = FormatDetector.DetectInputFile(args.InputFile);
            if (inputFormatResult.IsFailure)
            {
                await logger.WriteErrorAsync($"Error detecting input format: {inputFormatResult.Error}");
                return ExitCode.Failure;
            }

            var inputFormat = inputFormatResult.Value;

            // JSON Object/Array input requires a DrillDown-scoped recipe
            var validationResult = DrillDownRecipeValidator.Validate(inputFormat, recipe);
            if (validationResult.IsFailure)
            {
                await logger.WriteErrorAsync($"Error validating recipe: {validationResult.Error}");
                return ExitCode.Failure;
            }

            var outputFormatResult = FormatDetector.DetectOutputFile(args.OutputFile);
            if (outputFormatResult.IsFailure)
            {
                await logger.WriteErrorAsync($"Error detecting output format: {outputFormatResult.Error}");
                return ExitCode.Failure;
            }

            var outputFormat = outputFormatResult.Value;

            // Resolve the full input column name set (no type inference);
            // drillDownKeyPath is not yet sourced from the recipe
            var columnNamesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
                inputFormat, args.InputFile, drillDownKeyPath: null, ct).ConfigureAwait(false);
            if (columnNamesResult.IsFailure)
            {
                await logger.WriteErrorAsync($"Error resolving columns: {columnNamesResult.Error}");
                return ExitCode.Failure;
            }

            var columnNames = columnNamesResult.Value;

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
}

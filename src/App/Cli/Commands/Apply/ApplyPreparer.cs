using Refedle.Engine;
using Refedle.Engine.Models;
using Refedle.Engine.Recipes;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// The resolved state of the apply pipeline before any transformation or write happens.
/// Shared by <see cref="Runner"/> and <see cref="DryRunner"/>.
/// </summary>
/// <param name="Recipe">The loaded recipe.</param>
/// <param name="InputFormat">The format detected for the input file.</param>
/// <param name="OutputFormat">
/// The format detected for the output file, or null when no output file was given (dry run without <c>--output</c>).
/// </param>
/// <param name="ColumnNames">The full, ordered input column names resolved for the recipe's scope.</param>
/// <param name="OutputSchema">The output plan built from the input columns and the recipe's actions.</param>
internal sealed record ApplyPreparation(
    Recipe Recipe,
    DataFormat InputFormat,
    DataFormat? OutputFormat,
    IReadOnlyList<string> ColumnNames,
    BatchOutputSchema OutputSchema);

/// <summary>
/// Runs the preparation steps shared by the apply and dry-run pipelines:
/// recipe load → input format detection → DrillDown applicability validation →
/// output format detection (skipped without an output file) → column resolution →
/// output schema build. Each step's failure is logged exactly as <see cref="Runner"/>
/// has always reported it, so both pipelines emit identical error messages.
/// </summary>
internal static class ApplyPreparer
{
    /// <summary>
    /// Prepares the apply pipeline for the given files without transforming or writing any data.
    /// </summary>
    /// <param name="inputFile">The path to the input file.</param>
    /// <param name="recipeFile">The path to the recipe YAML file.</param>
    /// <param name="outputFile">
    /// The path to the output file, or null for a dry run without <c>--output</c>
    /// (output format detection is skipped).
    /// </param>
    /// <param name="logger">The app logger used to report step failures.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A successful <see cref="Result{T}"/> carrying the preparation, or a failure after logging.</returns>
    public static async ValueTask<Result<ApplyPreparation>> PrepareAsync(
        string inputFile,
        string recipeFile,
        string? outputFile,
        IAppLogger logger,
        CancellationToken ct)
    {
        var recipeResult = await new RecipeManager().LoadAsync(recipeFile, ct).ConfigureAwait(false);
        if (recipeResult.IsFailure)
        {
            return await LogAndFailAsync(logger, $"Error loading recipe: {recipeResult.Error}").ConfigureAwait(false);
        }

        var recipe = recipeResult.Value;

        var inputFormatResult = FormatDetector.DetectInputFile(inputFile);
        if (inputFormatResult.IsFailure)
        {
            return await LogAndFailAsync(logger, $"Error detecting input format: {inputFormatResult.Error}").ConfigureAwait(false);
        }

        var inputFormat = inputFormatResult.Value;

        // JSON Object/Array input requires a DrillDown-scoped recipe
        var validationResult = DrillDownRecipeValidator.Validate(inputFormat, recipe);
        if (validationResult.IsFailure)
        {
            return await LogAndFailAsync(logger, $"Error validating recipe: {validationResult.Error}").ConfigureAwait(false);
        }

        DataFormat? outputFormat = null;
        if (outputFile is not null)
        {
            var outputFormatResult = FormatDetector.DetectOutputFile(outputFile);
            if (outputFormatResult.IsFailure)
            {
                return await LogAndFailAsync(logger, $"Error detecting output format: {outputFormatResult.Error}").ConfigureAwait(false);
            }

            outputFormat = outputFormatResult.Value;
        }

        // Resolve the full input column name set (no type inference), scoped to the recipe's
        // DrillDown location when it has one (null for a base-table recipe).
        var columnNamesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
            inputFormat, inputFile, recipe.DrillDownKeyPath, ct).ConfigureAwait(false);
        if (columnNamesResult.IsFailure)
        {
            return await LogAndFailAsync(logger, $"Error resolving columns: {columnNamesResult.Error}").ConfigureAwait(false);
        }

        var columnNames = columnNamesResult.Value;

        var outputSchemaResult = ActionApplier.BuildOutputSchema(columnNames, recipe.Actions);
        if (outputSchemaResult.IsFailure)
        {
            return await LogAndFailAsync(logger, $"Error building output schema: {outputSchemaResult.Error}").ConfigureAwait(false);
        }

        return Results.Success(new ApplyPreparation(
            recipe, inputFormat, outputFormat, columnNames, outputSchemaResult.Value));
    }

    private static async ValueTask<Result<ApplyPreparation>> LogAndFailAsync(IAppLogger logger, string message)
    {
        await logger.WriteErrorAsync(message).ConfigureAwait(false);
        return Results.Failure<ApplyPreparation>(message);
    }
}

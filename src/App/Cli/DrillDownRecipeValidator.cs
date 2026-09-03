using Refedle.Engine;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.App.Cli;

/// <summary>
/// Validates that a recipe is applicable to the detected input format, before any
/// format-specific processing begins. JSON Object / JSON Array input requires a
/// DrillDown-scoped recipe; a recipe saved from the base table cannot be replayed
/// against it.
/// </summary>
internal static class DrillDownRecipeValidator
{
    /// <summary>
    /// Validates the loaded recipe against the detected input format.
    /// </summary>
    /// <param name="inputFormat">The format detected for the input file.</param>
    /// <param name="recipe">The loaded recipe.</param>
    /// <returns>
    /// Success, or a failure describing why the recipe cannot be applied to the input format.
    /// </returns>
    public static Result Validate(DataFormat inputFormat, Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        if (inputFormat is not (DataFormat.JsonObject or DataFormat.JsonArray))
        {
            return Results.Success();
        }

        return recipe.DrillDownKeyPath is null
            ? Results.Failure(
                $"Recipe '{recipe.Name}' has no DrillDown scope, but {inputFormat} input requires one")
            : Results.Success();
    }
}

using System.Text;
using Refedle.Engine.Models;

namespace Refedle.Engine.Recipes;

/// <summary>
/// Persists and restores <see cref="Recipe"/> objects as human-readable YAML files.
/// </summary>
public sealed class RecipeManager : IRecipeManager
{
    /// <inheritdoc/>
    public async ValueTask<Result<Recipe>> LoadAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return Results.Failure<Recipe>($"File not found: {filePath}");
        }

        try
        {
            string yaml = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct).ConfigureAwait(false);
            return RecipeYamlParser.Parse(yaml);
        }
        catch (IOException ex)
        {
            return Results.Failure<Recipe>(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<Result> SaveAsync(Recipe recipe, string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string yaml = RecipeYamlSerializer.Serialize(recipe);

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                yaml,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                ct
            ).ConfigureAwait(false);
            return Results.Success();
        }
        catch (IOException ex)
        {
            return Results.Failure(ex.Message);
        }
    }
}

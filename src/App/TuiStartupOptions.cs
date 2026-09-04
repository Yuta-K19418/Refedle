namespace Refedle.App;

/// <summary>
/// Holds optional startup arguments for TUI mode.
/// </summary>
internal sealed record TuiStartupOptions(string? InputFile = null, string? RecipeFile = null)
{
    public bool HasAny => InputFile is not null || RecipeFile is not null;

    /// <summary>
    /// Checks that every referenced startup file exists on disk.
    /// </summary>
    /// <returns>An error message for the first missing file, or <c>null</c> when all present paths resolve.</returns>
    public string? FindMissingFileError()
    {
        if (InputFile is not null && !File.Exists(InputFile))
        {
            return $"Error: File not found: {InputFile}";
        }

        if (RecipeFile is not null && !File.Exists(RecipeFile))
        {
            return $"Error: Recipe file not found: {RecipeFile}";
        }

        return null;
    }
}

namespace Refedle.App.Cli;

/// <summary>
/// Holds validated CLI argument values parsed from the command line.
/// </summary>
internal sealed record Arguments
{
    /// <summary>Gets the path to the input file.</summary>
    public required string InputFile { get; init; }

    /// <summary>Gets the path to the recipe YAML file.</summary>
    public required string RecipeFile { get; init; }

    /// <summary>
    /// Gets the path to the output file. Null is valid only for a dry run,
    /// where no output file is written.
    /// </summary>
    public string? OutputFile { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is a dry run: prepare and validate the
    /// pipeline, print the resolved plan, and write no output file.
    /// </summary>
    public bool IsDryRun { get; init; }
}

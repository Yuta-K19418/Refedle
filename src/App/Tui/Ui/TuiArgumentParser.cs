using Refedle.Engine;

namespace Refedle.App.Tui.Ui;

/// <summary>
/// Parses TUI-specific command-line arguments.
/// Accepts optional flags: <c>--file</c> and <c>--recipe</c>.
/// Unknown flags are rejected.
/// </summary>
internal static class TuiArgumentParser
{
    private const string FileFlag = "--file";
    private const string RecipeFlag = "--recipe";

    /// <summary>
    /// Parses the given command-line argument array into a <see cref="TuiStartupOptions"/> record.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the parsed arguments,
    /// or a failure with a human-readable error message.
    /// </returns>
    internal static Result<TuiStartupOptions> Parse(IReadOnlyList<string> args)
    {
        string? inputFile = null;
        string? recipeFile = null;

        var argsIndex = 0;
        while (argsIndex < args.Count)
        {
            var arg = args[argsIndex];

            if (arg == FileFlag)
            {
                argsIndex++;
                var consumeResult = ConsumeValue(args, argsIndex, FileFlag, inputFile);
                if (consumeResult.IsFailure)
                {
                    return Results.Failure<TuiStartupOptions>(consumeResult.Error);
                }

                inputFile = args[argsIndex++];
                continue;
            }

            if (arg == RecipeFlag)
            {
                argsIndex++;
                var consumeResult = ConsumeValue(args, argsIndex, RecipeFlag, recipeFile);
                if (consumeResult.IsFailure)
                {
                    return Results.Failure<TuiStartupOptions>(consumeResult.Error);
                }

                recipeFile = args[argsIndex++];
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                return Results.Failure<TuiStartupOptions>(
                    $"Unknown option: {arg}");
            }

            argsIndex++;
        }

        return Results.Success(new TuiStartupOptions(inputFile, recipeFile));
    }

    private static Result ConsumeValue(IReadOnlyList<string> args, int currentIndex, string flag, string? currentValue)
    {
        if (currentValue is not null)
        {
            return Results.Failure(
                $"{flag} specified more than once.");
        }

        if (currentIndex >= args.Count || args[currentIndex].StartsWith("--", StringComparison.Ordinal))
        {
            return Results.Failure(
                $"{flag} requires a value.");
        }

        return Results.Success();
    }
}

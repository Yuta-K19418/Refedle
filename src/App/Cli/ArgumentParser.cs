using Refedle.Engine;

namespace Refedle.App.Cli;

/// <summary>
/// Parses command-line arguments into an <see cref="Arguments"/> record.
/// Accepts named flags in the form <c>--key value</c>.
/// Required flags: <c>--input</c>, <c>--recipe</c>, <c>--output</c>.
/// Unknown flags are rejected.
/// </summary>
internal static partial class ArgumentParser
{
    private const string InputFlag = "--input";
    private const string RecipeFlag = "--recipe";
    private const string OutputFlag = "--output";

    /// <summary>
    /// Parses the given command-line argument array into an <see cref="Arguments"/> record.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the parsed arguments,
    /// or a failure with a human-readable error message.
    /// </returns>
    public static Result<Arguments> Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return Results.Failure<Arguments>("No arguments provided");
        }

        var result = new ArgumentsParseResult();
        var i = 0;

        while (i < args.Count)
        {
            var stepResult = ParseNextToken(args, i, result);
            if (stepResult.IsFailure)
            {
                return Results.Failure<Arguments>(stepResult.Error);
            }

            i = stepResult.Value;
        }

        return BuildArguments(result);
    }

    private static Result<int> ParseNextToken(IReadOnlyList<string> args, int i, ArgumentsParseResult result)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal))
        {
            return Results.Failure<int>($"Invalid flag: '{args[i]}'");
        }

        if (args[i].Equals(InputFlag, StringComparison.Ordinal))
        {
            return ConsumeValueFlag(args, i, InputFlag, value => result.InputFile = value);
        }

        if (args[i].Equals(RecipeFlag, StringComparison.Ordinal))
        {
            return ConsumeValueFlag(args, i, RecipeFlag, value => result.RecipeFile = value);
        }

        if (args[i].Equals(OutputFlag, StringComparison.Ordinal))
        {
            return ConsumeValueFlag(args, i, OutputFlag, value => result.OutputFile = value);
        }

        return Results.Failure<int>($"Unknown flag: {args[i]}");
    }

    private static Result<int> ConsumeValueFlag(IReadOnlyList<string> args, int i, string flag, Action<string> assign)
    {
        if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            return Results.Failure<int>($"Missing value for {flag}");
        }

        assign(args[i + 1]);
        return Results.Success(i + 2);
    }

    private static Result<Arguments> BuildArguments(ArgumentsParseResult result)
    {
        if (string.IsNullOrWhiteSpace(result.InputFile))
        {
            return Results.Failure<Arguments>($"Missing required flag: {InputFlag}");
        }

        if (string.IsNullOrWhiteSpace(result.RecipeFile))
        {
            return Results.Failure<Arguments>($"Missing required flag: {RecipeFlag}");
        }

        if (string.IsNullOrWhiteSpace(result.OutputFile))
        {
            return Results.Failure<Arguments>($"Missing required flag: {OutputFlag}");
        }

        return Results.Success(new Arguments
        {
            InputFile = result.InputFile,
            RecipeFile = result.RecipeFile,
            OutputFile = result.OutputFile,
        });
    }
}

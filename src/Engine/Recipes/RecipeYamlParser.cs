using System.Globalization;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;

namespace Refedle.Engine.Recipes;

/// <summary>
/// Parses YAML text into <see cref="Recipe"/> objects.
/// AOT-safe: no reflection is used.
/// </summary>
internal static class RecipeYamlParser
{
    /// <summary>
    /// Parses a YAML string into a recipe.
    /// Returns a failure result for any parse or validation error.
    /// </summary>
    public static Result<Recipe> Parse(string yaml)
    {
        var parseState = new RecipeYamlParseState(string.Empty, null, null, ParseState.Root, DrillDownKeyPathPresent: false);
        List<MorphAction> actions = [];
        List<KeyPathSegment> drillDownKeyPath = [];
        Dictionary<string, string> currentItem = [];

        foreach (var rawLine in yaml.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (IsSkippable(line))
            {
                continue;
            }

            var lineResult = ProcessLine(line, parseState, currentItem, actions, drillDownKeyPath);
            if (lineResult.IsFailure)
            {
                return Results.Failure<Recipe>(lineResult.Error);
            }

            (parseState, currentItem) = lineResult.Value;
        }

        var finalizeResult = FinalizePendingItem(parseState, currentItem, actions, drillDownKeyPath);
        if (finalizeResult.IsFailure)
        {
            return Results.Failure<Recipe>(finalizeResult.Error);
        }

        IReadOnlyList<KeyPathSegment>? parsedDrillDownKeyPath = parseState.DrillDownKeyPathPresent ? drillDownKeyPath.AsReadOnly() : null;

        return string.IsNullOrEmpty(parseState.Name)
            ? Results.Failure<Recipe>("Missing required field: 'name'")
            : Results.Success(new Recipe
            {
                Name = parseState.Name,
                Description = parseState.Description,
                LastModified = parseState.LastModified,
                Actions = actions.AsReadOnly(),
                DrillDownKeyPath = parsedDrillDownKeyPath,
            });
    }

    // Finalizes whichever item was still pending when the file ended, based on which
    // section was last active (actions vs. drillDownKeyPath).
    private static Result FinalizePendingItem(
        RecipeYamlParseState parseState,
        Dictionary<string, string> currentItem,
        List<MorphAction> actions,
        List<KeyPathSegment> drillDownKeyPath)
    {
        if (parseState.ParseState == ParseState.DrillDownKeyPathItem)
        {
            return DrillDownKeyPathSectionParser.FinalizePending(currentItem, drillDownKeyPath);
        }

        return ActionsSectionParser.FinalizePending(currentItem, actions);
    }

    // Dispatches a single YAML line to the handler for the current parse state.
    // currentItem is returned rather than mutated-in-place because item-boundary
    // transitions replace it wholesale with a fresh dictionary for the next item.
    private static Result<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)> ProcessLine(
        string line,
        RecipeYamlParseState parseState,
        Dictionary<string, string> currentItem,
        List<MorphAction> actions,
        List<KeyPathSegment> drillDownKeyPath)
    {
        if (parseState.ParseState == ParseState.Root)
        {
            var result = ProcessRootLine(line, parseState);
            return result.IsFailure
                ? Results.Failure<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)>(result.Error)
                : Results.Success((result.Value, currentItem));
        }

        if (parseState.ParseState == ParseState.DrillDownKeyPathItem)
        {
            return DrillDownKeyPathSectionParser.ProcessLine(line, parseState, currentItem, drillDownKeyPath);
        }

        return ActionsSectionParser.ProcessLine(line, parseState, currentItem, actions);
    }

    private static bool IsSkippable(string line)
        => string.IsNullOrWhiteSpace(line) || line.AsSpan().TrimStart().StartsWith("#", StringComparison.Ordinal);

    private static Result<RecipeYamlParseState> ProcessRootLine(string line, RecipeYamlParseState state)
    {
        if (line == "actions: []")
        {
            return Results.Success(state);
        }

        if (line == "actions:")
        {
            return Results.Success(state with { ParseState = ParseState.Actions });
        }

        if (line == "drillDownKeyPath: []")
        {
            return Results.Success(state with { DrillDownKeyPathPresent = true });
        }

        if (line == "drillDownKeyPath:")
        {
            return Results.Success(state with { ParseState = ParseState.DrillDownKeyPathItem, DrillDownKeyPathPresent = true });
        }

        var colonIdx = line.IndexOf(": ", StringComparison.Ordinal);
        if (colonIdx < 0)
        {
            return Results.Failure<RecipeYamlParseState>($"Malformed root-level line: '{line}'");
        }

        var key = line[..colonIdx];
        var value = line[(colonIdx + 2)..];

        return key switch
        {
            "name" => SetName(state, value),
            "description" => SetDescription(state, value),
            "lastModified" => SetLastModified(state, value),
            _ => Results.Failure<RecipeYamlParseState>($"Unknown root-level key: '{key}'"),
        };
    }

    private static Result<RecipeYamlParseState> SetName(RecipeYamlParseState state, string value) =>
        !string.IsNullOrEmpty(state.Name)
            ? Results.Failure<RecipeYamlParseState>("Duplicate root-level key: 'name'")
            : Results.Success(state with { Name = RecipeYamlFieldParser.UnquoteString(value) });

    private static Result<RecipeYamlParseState> SetDescription(RecipeYamlParseState state, string value) =>
        state.Description is not null
            ? Results.Failure<RecipeYamlParseState>("Duplicate root-level key: 'description'")
            : Results.Success(state with { Description = RecipeYamlFieldParser.UnquoteString(value) });

    private static Result<RecipeYamlParseState> SetLastModified(RecipeYamlParseState state, string value) =>
        state.LastModified is not null
            ? Results.Failure<RecipeYamlParseState>("Duplicate root-level key: 'lastModified'")
            : ParseLastModifiedField(value, state);

    private static Result<RecipeYamlParseState> ParseLastModifiedField(string value, RecipeYamlParseState state)
    {
        var parseResult = TryParseLastModified(value);
        if (parseResult.IsFailure)
        {
            return Results.Failure<RecipeYamlParseState>(parseResult.Error);
        }

        return Results.Success(state with { LastModified = parseResult.Value });
    }

    private static Result<DateTimeOffset> TryParseLastModified(string value)
    {
        if (!DateTimeOffset.TryParse(RecipeYamlFieldParser.UnquoteString(value), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
        {
            return Results.Failure<DateTimeOffset>($"Invalid lastModified value: '{value}'");
        }

        return Results.Success(dt);
    }
}

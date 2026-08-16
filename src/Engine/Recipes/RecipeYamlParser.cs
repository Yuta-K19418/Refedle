using System.Globalization;
using System.Text;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;

namespace Refedle.Engine.Recipes;

/// <summary>
/// Parses YAML text into <see cref="Recipe"/> objects.
/// AOT-safe: no reflection is used.
/// </summary>
internal sealed class RecipeYamlParser
{
    private sealed record RootParseState(string Name, string? Description, DateTimeOffset? LastModified, ParseState ParseState);

    /// <summary>
    /// Parses a YAML string into a recipe.
    /// Returns a failure result for any parse or validation error.
    /// </summary>
    public static Result<Recipe> Parse(string yaml)
    {
        var rootState = new RootParseState(string.Empty, null, null, ParseState.Root);
        List<MorphAction> actions = [];
        Dictionary<string, string> currentAction = [];

        foreach (var rawLine in yaml.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (IsSkippable(line))
            {
                continue;
            }

            var lineResult = ProcessLine(line, rootState, currentAction, actions);
            if (lineResult.IsFailure)
            {
                return Results.Failure<Recipe>(lineResult.Error);
            }

            (rootState, currentAction) = lineResult.Value;
        }

        if (currentAction.ContainsKey("type"))
        {
            var buildResult = MorphActionParser.ParseAction(currentAction);
            if (buildResult.IsFailure)
            {
                return Results.Failure<Recipe>(buildResult.Error);
            }

            actions.Add(buildResult.Value);
        }

        return string.IsNullOrEmpty(rootState.Name)
            ? Results.Failure<Recipe>("Missing required field: 'name'")
            : Results.Success(new Recipe
            {
                Name = rootState.Name,
                Description = rootState.Description,
                LastModified = rootState.LastModified,
                Actions = actions.AsReadOnly(),
            });
    }

    // Dispatches a single YAML line to the handler for the current parse state.
    // currentAction is returned rather than mutated-in-place because StartNewAction
    // replaces it wholesale with a fresh dictionary for the next action item.
    private static Result<(RootParseState rootState, Dictionary<string, string> currentAction)> ProcessLine(
        string line,
        RootParseState rootState,
        Dictionary<string, string> currentAction,
        List<MorphAction> actions)
    {
        if (rootState.ParseState == ParseState.Root)
        {
            var result = ProcessRootLine(line, rootState);
            return result.IsFailure
                ? Results.Failure<(RootParseState rootState, Dictionary<string, string> currentAction)>(result.Error)
                : Results.Success((result.Value, currentAction));
        }

        if (line.StartsWith("  - type: ", StringComparison.Ordinal))
        {
            var startResult = StartNewAction(line, currentAction);
            if (startResult.IsFailure)
            {
                return Results.Failure<(RootParseState rootState, Dictionary<string, string> currentAction)>(startResult.Error);
            }

            var (newCurrentAction, completedAction) = startResult.Value;
            if (completedAction is not null)
            {
                actions.Add(completedAction);
            }

            return Results.Success((rootState with { ParseState = ParseState.ActionItem }, newCurrentAction));
        }

        if (rootState.ParseState != ParseState.ActionItem || !line.StartsWith("    ", StringComparison.Ordinal))
        {
            return Results.Failure<(RootParseState rootState, Dictionary<string, string> currentAction)>(
                $"Unexpected line in actions context: '{line}'");
        }

        var fieldResult = ParseActionField(line);
        if (fieldResult.IsFailure)
        {
            return Results.Failure<(RootParseState rootState, Dictionary<string, string> currentAction)>(fieldResult.Error);
        }

        var (fieldKey, fieldValue) = fieldResult.Value;
        currentAction[fieldKey] = fieldValue;
        return Results.Success((rootState, currentAction));
    }

    private static bool IsSkippable(string line)
        => string.IsNullOrWhiteSpace(line) || line.AsSpan().TrimStart().StartsWith("#", StringComparison.Ordinal);

    private static Result<RootParseState> ProcessRootLine(string line, RootParseState state)
    {
        if (line == "actions: []")
        {
            return Results.Success(state);
        }

        if (line == "actions:")
        {
            return Results.Success(state with { ParseState = ParseState.Actions });
        }

        var colonIdx = line.IndexOf(": ", StringComparison.Ordinal);
        if (colonIdx < 0)
        {
            return Results.Failure<RootParseState>($"Malformed root-level line: '{line}'");
        }

        var key = line[..colonIdx];
        var value = line[(colonIdx + 2)..];

        return key switch
        {
            "name" => SetName(state, value),
            "description" => SetDescription(state, value),
            "lastModified" => SetLastModified(state, value),
            _ => Results.Failure<RootParseState>($"Unknown root-level key: '{key}'"),
        };
    }

    private static Result<RootParseState> SetName(RootParseState state, string value) =>
        !string.IsNullOrEmpty(state.Name)
            ? Results.Failure<RootParseState>("Duplicate root-level key: 'name'")
            : Results.Success(state with { Name = UnquoteString(value) });

    private static Result<RootParseState> SetDescription(RootParseState state, string value) =>
        state.Description is not null
            ? Results.Failure<RootParseState>("Duplicate root-level key: 'description'")
            : Results.Success(state with { Description = UnquoteString(value) });

    private static Result<RootParseState> SetLastModified(RootParseState state, string value) =>
        state.LastModified is not null
            ? Results.Failure<RootParseState>("Duplicate root-level key: 'lastModified'")
            : ParseLastModifiedField(value, state);

    private static Result<RootParseState> ParseLastModifiedField(string value, RootParseState state)
    {
        var parseResult = TryParseLastModified(value);
        if (parseResult.IsFailure)
        {
            return Results.Failure<RootParseState>(parseResult.Error);
        }

        return Results.Success(state with { LastModified = parseResult.Value });
    }

    private static Result<DateTimeOffset> TryParseLastModified(string value)
    {
        if (!DateTimeOffset.TryParse(UnquoteString(value), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
        {
            return Results.Failure<DateTimeOffset>($"Invalid lastModified value: '{value}'");
        }

        return Results.Success(dt);
    }

    private static Result<(Dictionary<string, string> newAction, MorphAction? completedAction)> StartNewAction(
        string line,
        Dictionary<string, string> currentAction)
    {
        MorphAction? completedAction = null;
        if (currentAction.ContainsKey("type"))
        {
            var parseResult = MorphActionParser.ParseAction(currentAction);
            if (parseResult.IsFailure)
            {
                return Results.Failure<(Dictionary<string, string> newAction, MorphAction? completedAction)>(parseResult.Error);
            }

            completedAction = parseResult.Value;
        }

        var newAction = new Dictionary<string, string>(StringComparer.Ordinal) { ["type"] = line["  - type: ".Length..] };
        return Results.Success<(Dictionary<string, string> newAction, MorphAction? completedAction)>((newAction, completedAction));
    }

    private static Result<(string key, string value)> ParseActionField(string line)
    {
        var fieldContent = line[4..];
        var colonIdx = fieldContent.IndexOf(": ", StringComparison.Ordinal);
        if (colonIdx < 0)
        {
            return Results.Failure<(string key, string value)>($"Malformed action field: '{line}'");
        }

        return Results.Success((fieldContent[..colonIdx], UnquoteString(fieldContent[(colonIdx + 2)..])));
    }

    private static string UnquoteString(string value)
    {
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return value;
        }

        var inner = value.AsSpan(1, value.Length - 2);
        return inner.IndexOf('\\') < 0 ? inner.ToString() : UnescapeString(inner);
    }

    private static string UnescapeString(ReadOnlySpan<char> inner)
    {
        var sb = new StringBuilder(inner.Length);
        var i = 0;
        while (i < inner.Length)
        {
            if (inner[i] == '\\' && i + 1 < inner.Length)
            {
                sb.Append(inner[i + 1] switch
                {
                    '"' => '"',
                    '\\' => '\\',
                    var c => c,
                });
                i += 2;
                continue;
            }

            sb.Append(inner[i]);
            i++;
        }

        return sb.ToString();
    }
}

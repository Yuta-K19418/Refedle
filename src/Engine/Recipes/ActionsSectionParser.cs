using Refedle.Engine.Models.Actions;

namespace Refedle.Engine.Recipes;

// Actions-section line processing. Also owns the transitions out of the actions section
// into drillDownKeyPath parsing, since they only flush the pending action and flip
// ParseState — they never touch DrillDownKeyPathSectionParser.
internal static class ActionsSectionParser
{
    internal static Result<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)> ProcessLine(
        string line,
        RecipeYamlParseState parseState,
        Dictionary<string, string> currentAction,
        List<MorphAction> actions)
    {
        if (line == "drillDownKeyPath: []")
        {
            return TransitionToEmptyDrillDownKeyPath(parseState, currentAction, actions);
        }

        if (line == "drillDownKeyPath:")
        {
            return TransitionToDrillDownKeyPath(parseState, currentAction, actions);
        }

        if (line.StartsWith("  - type: ", StringComparison.Ordinal))
        {
            var startResult = StartNewAction(line, currentAction);
            if (startResult.IsFailure)
            {
                return Results.Failure<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)>(startResult.Error);
            }

            var (newCurrentAction, completedAction) = startResult.Value;
            if (completedAction is not null)
            {
                actions.Add(completedAction);
            }

            return Results.Success((parseState with { ParseState = ParseState.ActionItem }, newCurrentAction));
        }

        if (parseState.ParseState != ParseState.ActionItem || !line.StartsWith("    ", StringComparison.Ordinal))
        {
            return Results.Failure<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)>(
                $"Unexpected line in actions context: '{line}'");
        }

        var fieldResult = RecipeYamlFieldParser.ParseField(line[4..]);
        if (fieldResult.IsFailure)
        {
            return Results.Failure<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)>(
                $"Malformed action field: '{line}'");
        }

        var (fieldKey, fieldValue) = fieldResult.Value;
        currentAction[fieldKey] = fieldValue;
        return Results.Success((parseState, currentAction));
    }

    // Parses currentAction into a MorphAction and appends it, if a "type" field was collected;
    // a no-op otherwise. Shared by end-of-file finalization and the drillDownKeyPath transitions,
    // both of which must flush whatever action was still open before moving on.
    internal static Result FinalizePending(Dictionary<string, string> currentAction, List<MorphAction> actions)
    {
        if (!currentAction.ContainsKey("type"))
        {
            return Results.Success();
        }

        var actionResult = MorphActionParser.ParseAction(currentAction);
        if (actionResult.IsFailure)
        {
            return Results.Failure(actionResult.Error);
        }

        actions.Add(actionResult.Value);
        return Results.Success();
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

    // Ends the actions section (finalizing whatever action was pending, if any) and
    // switches to accumulating drillDownKeyPath segment items.
    private static Result<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)> TransitionToDrillDownKeyPath(
        RecipeYamlParseState parseState,
        Dictionary<string, string> currentAction,
        List<MorphAction> actions)
    {
        var finalizeResult = FinalizePending(currentAction, actions);
        if (finalizeResult.IsFailure)
        {
            return Results.Failure<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)>(finalizeResult.Error);
        }

        return Results.Success((
            parseState with { ParseState = ParseState.DrillDownKeyPathItem, DrillDownKeyPathPresent = true },
            new Dictionary<string, string>(StringComparer.Ordinal)));
    }

    // Ends the actions section for a "drillDownKeyPath: []" declaration: finalizes whatever
    // action was pending, records that the section was present, but collects no segments.
    private static Result<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)> TransitionToEmptyDrillDownKeyPath(
        RecipeYamlParseState parseState,
        Dictionary<string, string> currentAction,
        List<MorphAction> actions)
    {
        var finalizeResult = FinalizePending(currentAction, actions);
        if (finalizeResult.IsFailure)
        {
            return Results.Failure<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)>(finalizeResult.Error);
        }

        return Results.Success((
            parseState with { ParseState = ParseState.Root, DrillDownKeyPathPresent = true },
            new Dictionary<string, string>(StringComparer.Ordinal)));
    }
}

using System.Globalization;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models.Actions;

namespace Refedle.Engine.Recipes;

internal sealed partial class RecipeYamlParser
{
    // Ends the actions section (finalizing whatever action was pending, if any) and
    // switches to accumulating drillDownKeyPath segment items.
    private static Result<(RootParseState rootState, Dictionary<string, string> currentItem)> TransitionToDrillDownKeyPath(
        RootParseState rootState,
        Dictionary<string, string> currentAction,
        List<MorphAction> actions)
    {
        var finalizeResult = FinalizePendingAction(currentAction, actions);
        if (finalizeResult.IsFailure)
        {
            return Results.Failure<(RootParseState rootState, Dictionary<string, string> currentItem)>(finalizeResult.Error);
        }

        return Results.Success((
            rootState with { ParseState = ParseState.DrillDownKeyPathItem, DrillDownKeyPathPresent = true },
            new Dictionary<string, string>(StringComparer.Ordinal)));
    }

    // Ends the actions section for a "drillDownKeyPath: []" declaration: finalizes whatever
    // action was pending, records that the section was present, but collects no segments.
    private static Result<(RootParseState rootState, Dictionary<string, string> currentItem)> TransitionToEmptyDrillDownKeyPath(
        RootParseState rootState,
        Dictionary<string, string> currentAction,
        List<MorphAction> actions)
    {
        var finalizeResult = FinalizePendingAction(currentAction, actions);
        if (finalizeResult.IsFailure)
        {
            return Results.Failure<(RootParseState rootState, Dictionary<string, string> currentItem)>(finalizeResult.Error);
        }

        return Results.Success((
            rootState with { ParseState = ParseState.Root, DrillDownKeyPathPresent = true },
            new Dictionary<string, string>(StringComparer.Ordinal)));
    }

    private static Result<(RootParseState rootState, Dictionary<string, string> currentItem)> ProcessDrillDownKeyPathLine(
        string line,
        RootParseState rootState,
        Dictionary<string, string> currentItem,
        List<KeyPathSegment> drillDownKeyPath)
    {
        if (line.StartsWith("  - ", StringComparison.Ordinal))
        {
            return StartNewDrillDownKeyPathItem(line, rootState, currentItem, drillDownKeyPath);
        }

        if (!line.StartsWith("    ", StringComparison.Ordinal))
        {
            return Results.Failure<(RootParseState rootState, Dictionary<string, string> currentItem)>(
                $"Unexpected line in drillDownKeyPath context: '{line}'");
        }

        var fieldResult = ParseActionField(line);
        if (fieldResult.IsFailure)
        {
            return Results.Failure<(RootParseState rootState, Dictionary<string, string> currentItem)>(fieldResult.Error);
        }

        var (fieldKey, fieldValue) = fieldResult.Value;
        currentItem[fieldKey] = fieldValue;
        return Results.Success((rootState, currentItem));
    }

    // An item boundary ("  - " prefix): finalizes the previous item (if one was open),
    // then starts a fresh one from this line's first field (either "key" or "index").
    private static Result<(RootParseState rootState, Dictionary<string, string> currentItem)> StartNewDrillDownKeyPathItem(
        string line,
        RootParseState rootState,
        Dictionary<string, string> currentItem,
        List<KeyPathSegment> drillDownKeyPath)
    {
        if (currentItem.Count > 0)
        {
            var segmentsResult = ParseDrillDownKeyPathItem(currentItem);
            if (segmentsResult.IsFailure)
            {
                return Results.Failure<(RootParseState rootState, Dictionary<string, string> currentItem)>(segmentsResult.Error);
            }

            drillDownKeyPath.AddRange(segmentsResult.Value);
        }

        var fieldResult = ParseActionField(line);
        if (fieldResult.IsFailure)
        {
            return Results.Failure<(RootParseState rootState, Dictionary<string, string> currentItem)>(fieldResult.Error);
        }

        var (fieldKey, fieldValue) = fieldResult.Value;
        return Results.Success((rootState, new Dictionary<string, string>(StringComparer.Ordinal) { [fieldKey] = fieldValue }));
    }

    // Finalizes the drillDownKeyPath item pending when the file ended (mirrors the
    // action-finalization branch in FinalizePendingItem, for the other section kind).
    private static Result FinalizeDrillDownKeyPathItem(Dictionary<string, string> currentItem, List<KeyPathSegment> drillDownKeyPath)
    {
        var segmentsResult = ParseDrillDownKeyPathItem(currentItem);
        if (segmentsResult.IsFailure)
        {
            return Results.Failure(segmentsResult.Error);
        }

        drillDownKeyPath.AddRange(segmentsResult.Value);
        return Results.Success();
    }

    // Converts one drillDownKeyPath item's fields into 1-2 KeyPathSegments: "key" alone
    // yields a Key segment, "index" alone yields an Index segment, both yields a Key
    // segment immediately followed by an Index segment (key always precedes index).
    private static Result<IReadOnlyList<KeyPathSegment>> ParseDrillDownKeyPathItem(Dictionary<string, string> item)
    {
        var hasIndex = item.TryGetValue("index", out var indexValue);

        if (!item.ContainsKey("key") && !hasIndex)
        {
            return Results.Failure<IReadOnlyList<KeyPathSegment>>("DrillDownKeyPath item missing both 'key' and 'index'");
        }

        var indexNumber = 0;
        if (hasIndex && !int.TryParse(indexValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out indexNumber))
        {
            return Results.Failure<IReadOnlyList<KeyPathSegment>>($"Invalid DrillDownKeyPath index value: '{indexValue}'");
        }

        List<KeyPathSegment> segments = [];
        if (item.TryGetValue("key", out var key))
        {
            segments.Add(new KeyPathSegment(key, KeyPathSegmentKind.Key));
        }

        if (hasIndex)
        {
            segments.Add(new KeyPathSegment(string.Create(CultureInfo.InvariantCulture, $"[{indexNumber}]"), KeyPathSegmentKind.Index));
        }

        return Results.Success<IReadOnlyList<KeyPathSegment>>(segments);
    }
}

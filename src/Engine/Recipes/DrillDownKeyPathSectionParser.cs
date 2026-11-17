using System.Globalization;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.Engine.Recipes;

// drillDownKeyPath-section line processing: accumulates "  - key: ..." / "index: ..." items
// into KeyPathSegments.
internal static class DrillDownKeyPathSectionParser
{
    internal static Result<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)> ProcessLine(
        string line,
        RecipeYamlParseState parseState,
        Dictionary<string, string> currentItem,
        List<KeyPathSegment> drillDownKeyPath)
    {
        if (line.StartsWith("  - ", StringComparison.Ordinal))
        {
            return StartNewItem(line, parseState, currentItem, drillDownKeyPath);
        }

        if (!line.StartsWith("    ", StringComparison.Ordinal))
        {
            return Results.Failure<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)>(
                $"Unexpected line in drillDownKeyPath context: '{line}'");
        }

        Result<(string key, string value)> fieldResult = RecipeYamlFieldParser.ParseField(line[4..]);
        if (fieldResult.IsFailure)
        {
            return Results.Failure<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)>(
                $"Malformed action field: '{line}'");
        }

        (string? fieldKey, string? fieldValue) = fieldResult.Value;
        currentItem[fieldKey] = fieldValue;
        return Results.Success((parseState, currentItem));
    }

    // Finalizes the drillDownKeyPath item pending when the file ended (mirrors
    // ActionsSectionParser.FinalizePending for the other section kind).
    internal static Result FinalizePending(Dictionary<string, string> currentItem, List<KeyPathSegment> drillDownKeyPath)
    {
        Result<IReadOnlyList<KeyPathSegment>> segmentsResult = ParseItem(currentItem);
        if (segmentsResult.IsFailure)
        {
            return Results.Failure(segmentsResult.Error);
        }

        drillDownKeyPath.AddRange(segmentsResult.Value);
        return Results.Success();
    }

    // An item boundary ("  - " prefix): finalizes the previous item (if one was open),
    // then starts a fresh one from this line's first field (either "key" or "index").
    private static Result<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)> StartNewItem(
        string line,
        RecipeYamlParseState parseState,
        Dictionary<string, string> currentItem,
        List<KeyPathSegment> drillDownKeyPath)
    {
        if (currentItem.Count > 0)
        {
            Result<IReadOnlyList<KeyPathSegment>> segmentsResult = ParseItem(currentItem);
            if (segmentsResult.IsFailure)
            {
                return Results.Failure<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)>(segmentsResult.Error);
            }

            drillDownKeyPath.AddRange(segmentsResult.Value);
        }

        Result<(string key, string value)> fieldResult = RecipeYamlFieldParser.ParseField(line[4..]);
        if (fieldResult.IsFailure)
        {
            return Results.Failure<(RecipeYamlParseState parseState, Dictionary<string, string> currentItem)>(
                $"Malformed action field: '{line}'");
        }

        (string? fieldKey, string? fieldValue) = fieldResult.Value;
        return Results.Success((parseState, new Dictionary<string, string>(StringComparer.Ordinal) { [fieldKey] = fieldValue }));
    }

    // Converts one drillDownKeyPath item's fields into 1-2 KeyPathSegments: "key" alone
    // yields a Key segment, "index" alone yields an Index segment, both yields a Key
    // segment immediately followed by an Index segment (key always precedes index).
    private static Result<IReadOnlyList<KeyPathSegment>> ParseItem(Dictionary<string, string> item)
    {
        bool hasIndex = item.TryGetValue("index", out string? indexValue);

        if (!item.ContainsKey("key") && !hasIndex)
        {
            return Results.Failure<IReadOnlyList<KeyPathSegment>>("DrillDownKeyPath item missing both 'key' and 'index'");
        }

        int indexNumber = 0;
        if (hasIndex && !int.TryParse(indexValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out indexNumber))
        {
            return Results.Failure<IReadOnlyList<KeyPathSegment>>($"Invalid DrillDownKeyPath index value: '{indexValue}'");
        }

        List<KeyPathSegment> segments = [];
        if (item.TryGetValue("key", out string? key))
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

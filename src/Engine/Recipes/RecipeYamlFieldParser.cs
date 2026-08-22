using Refedle.Engine.Utilities;

namespace Refedle.Engine.Recipes;

// State-independent scalar/field parsing helpers shared by RecipeYamlParser and both section
// parsers. Takes already-unindented content: callers strip their own nested-item indentation
// before calling ParseField, since indentation width differs by parsing context.
internal static class RecipeYamlFieldParser
{
    internal static Result<(string key, string value)> ParseField(string fieldContent)
    {
        var colonIdx = fieldContent.IndexOf(": ", StringComparison.Ordinal);
        if (colonIdx < 0)
        {
            return Results.Failure<(string key, string value)>($"Malformed field: '{fieldContent}'");
        }

        return Results.Success((fieldContent[..colonIdx], StringUtility.UnquoteString(fieldContent[(colonIdx + 2)..])));
    }
}

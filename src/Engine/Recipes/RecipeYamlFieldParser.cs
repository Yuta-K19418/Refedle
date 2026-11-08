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
        return colonIdx < 0
            ? Results.Failure<(string key, string value)>($"Malformed field: '{fieldContent}'")
            : (Result<(string key, string value)>)Results.Success((fieldContent[..colonIdx], StringUtility.UnquoteString(fieldContent[(colonIdx + 2)..])));
    }
}

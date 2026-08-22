using System.Text;

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

        return Results.Success((fieldContent[..colonIdx], UnquoteString(fieldContent[(colonIdx + 2)..])));
    }

    internal static string UnquoteString(string value)
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

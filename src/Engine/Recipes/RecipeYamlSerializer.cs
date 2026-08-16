using System.Diagnostics;
using System.Globalization;
using System.Text;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;

namespace Refedle.Engine.Recipes;

/// <summary>
/// Serializes and deserializes <see cref="Recipe"/> objects to and from YAML.
/// AOT-safe: no reflection is used.
/// </summary>
internal static class RecipeYamlSerializer
{
    /// <summary>
    /// Serializes a recipe to a YAML string.
    /// </summary>
    public static string Serialize(Recipe recipe)
    {
        var sb = new StringBuilder();
        sb.Append("name: ").AppendLine(QuoteString(recipe.Name));

        if (recipe.Description is not null)
        {
            sb.Append("description: ").AppendLine(QuoteString(recipe.Description));
        }

        if (recipe.LastModified is not null)
        {
            sb.Append("lastModified: ").AppendLine(QuoteString(recipe.LastModified.Value.ToString("O")));
        }

        if (recipe.Actions.Count == 0)
        {
            sb.AppendLine("actions: []");
            return sb.ToString();
        }

        sb.AppendLine("actions:");
        foreach (var action in recipe.Actions)
        {
            AppendAction(sb, action);
        }

        return sb.ToString();
    }

    private static void AppendAction(StringBuilder sb, MorphAction action)
    {
        switch (action)
        {
            case RenameColumnAction rename:
                sb.AppendLine("  - type: rename");
                sb.Append("    oldName: ").AppendLine(QuoteString(rename.OldName));
                sb.Append("    newName: ").AppendLine(QuoteString(rename.NewName));
                break;
            case DeleteColumnAction delete:
                sb.AppendLine("  - type: delete");
                sb.Append("    columnName: ").AppendLine(QuoteString(delete.ColumnName));
                break;
            case CastColumnAction cast:
                sb.AppendLine("  - type: cast");
                sb.Append("    columnName: ").AppendLine(QuoteString(cast.ColumnName));
                sb.AppendLine(CultureInfo.InvariantCulture, $"    targetType: {cast.TargetType}");
                break;
            case FilterAction filter:
                sb.AppendLine("  - type: filter");
                sb.Append("    columnName: ").AppendLine(QuoteString(filter.ColumnName));
                sb.AppendLine(CultureInfo.InvariantCulture, $"    operator: {filter.Operator}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    comparisonType: {filter.ComparisonType}");
                sb.Append("    value: ").AppendLine(QuoteString(filter.Value));
                break;
            case FillColumnAction fill:
                sb.AppendLine("  - type: fill");
                sb.Append("    columnName: ").AppendLine(QuoteString(fill.ColumnName));
                sb.Append("    value: ").AppendLine(QuoteString(fill.Value));
                break;
            case FormatTimestampAction formatTimestamp:
                sb.AppendLine("  - type: format_timestamp");
                sb.Append("    columnName: ").AppendLine(QuoteString(formatTimestamp.ColumnName));
                sb.Append("    targetFormat: ").AppendLine(QuoteString(formatTimestamp.TargetFormat));
                break;
            default:
                throw new UnreachableException("Unhandled MorphAction subtype in serializer");
        }
    }

    private static string QuoteString(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}

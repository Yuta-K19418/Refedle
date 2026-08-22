using System.Diagnostics;
using System.Globalization;
using System.Text;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Utilities;

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
        sb.Append("name: ").AppendLine(StringUtility.QuoteString(recipe.Name));

        if (recipe.Description is not null)
        {
            sb.Append("description: ").AppendLine(StringUtility.QuoteString(recipe.Description));
        }

        if (recipe.LastModified is not null)
        {
            sb.Append("lastModified: ").AppendLine(recipe.LastModified.Value.ToString("O"));
        }

        AppendActions(sb, recipe.Actions);

        if (recipe.DrillDownKeyPath is { } drillDownKeyPath)
        {
            AppendDrillDownKeyPath(sb, drillDownKeyPath);
        }

        return sb.ToString();
    }

    private static void AppendActions(StringBuilder sb, IReadOnlyList<MorphAction> actions)
    {
        if (actions.Count == 0)
        {
            sb.AppendLine("actions: []");
            return;
        }

        sb.AppendLine("actions:");
        foreach (var action in actions)
        {
            AppendAction(sb, action);
        }
    }

    private static void AppendDrillDownKeyPath(StringBuilder sb, IReadOnlyList<KeyPathSegment> segments)
    {
        // An empty KeyPath is legitimate: a root-level Full Aggregation DrillDown selecting a
        // top-level element directly has no segments (see KeyPathTraverser.LastKeySegment).
        if (segments.Count == 0)
        {
            sb.AppendLine("drillDownKeyPath: []");
            return;
        }

        sb.AppendLine("drillDownKeyPath:");

        var i = 0;
        while (i < segments.Count)
        {
            var segment = segments[i];
            if (segment.Kind == KeyPathSegmentKind.Key && i + 1 < segments.Count && segments[i + 1].Kind == KeyPathSegmentKind.Index)
            {
                sb.Append("  - key: ").AppendLine(StringUtility.QuoteString(segment.Value));
                sb.Append("    index: ").AppendLine(ExtractIndexLabel(segments[i + 1].Value));
                i += 2;
                continue;
            }

            if (segment.Kind == KeyPathSegmentKind.Key)
            {
                sb.Append("  - key: ").AppendLine(StringUtility.QuoteString(segment.Value));
                i++;
                continue;
            }

            sb.Append("  - index: ").AppendLine(ExtractIndexLabel(segment.Value));
            i++;
        }
    }

    // Index-kind KeyPathSegment.Value is always in "[N]" form (see KeyPathSegment's doc comment).
    private static string ExtractIndexLabel(string value) => value[1..^1];

    private static void AppendAction(StringBuilder sb, MorphAction action)
    {
        switch (action)
        {
            case RenameColumnAction rename:
                sb.AppendLine("  - type: Rename");
                sb.Append("    oldName: ").AppendLine(StringUtility.QuoteString(rename.OldName));
                sb.Append("    newName: ").AppendLine(StringUtility.QuoteString(rename.NewName));
                break;
            case DeleteColumnAction delete:
                sb.AppendLine("  - type: Delete");
                sb.Append("    columnName: ").AppendLine(StringUtility.QuoteString(delete.ColumnName));
                break;
            case CastColumnAction cast:
                sb.AppendLine("  - type: Cast");
                sb.Append("    columnName: ").AppendLine(StringUtility.QuoteString(cast.ColumnName));
                sb.AppendLine(CultureInfo.InvariantCulture, $"    targetType: {cast.TargetType}");
                break;
            case FilterAction filter:
                sb.AppendLine("  - type: Filter");
                sb.Append("    columnName: ").AppendLine(StringUtility.QuoteString(filter.ColumnName));
                sb.AppendLine(CultureInfo.InvariantCulture, $"    operator: {filter.Operator}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    comparisonType: {filter.ComparisonType}");
                sb.Append("    value: ").AppendLine(StringUtility.QuoteString(filter.Value));
                break;
            case FillColumnAction fill:
                sb.AppendLine("  - type: Fill");
                sb.Append("    columnName: ").AppendLine(StringUtility.QuoteString(fill.ColumnName));
                sb.Append("    value: ").AppendLine(StringUtility.QuoteString(fill.Value));
                break;
            case FormatTimestampAction formatTimestamp:
                sb.AppendLine("  - type: FormatTimestamp");
                sb.Append("    columnName: ").AppendLine(StringUtility.QuoteString(formatTimestamp.ColumnName));
                sb.Append("    targetFormat: ").AppendLine(StringUtility.QuoteString(formatTimestamp.TargetFormat));
                break;
            default:
                throw new UnreachableException("Unhandled MorphAction subtype in serializer");
        }
    }
}

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
    // Recipe YAML always uses LF-only line endings, regardless of OS (StringBuilder.AppendLine
    // would use Environment.NewLine, producing CRLF on Windows).
    private const char NewLine = '\n';

    /// <summary>
    /// Serializes a recipe to a YAML string.
    /// </summary>
    public static string Serialize(Recipe recipe)
    {
        var sb = new StringBuilder();
        sb.Append("name: ").Append(StringUtility.QuoteString(recipe.Name)).Append(NewLine);

        if (recipe.Description is not null)
        {
            sb.Append("description: ").Append(StringUtility.QuoteString(recipe.Description)).Append(NewLine);
        }

        if (recipe.LastModified is not null)
        {
            sb.Append("lastModified: ").Append(recipe.LastModified.Value.ToString("O")).Append(NewLine);
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
            sb.Append("actions: []").Append(NewLine);
            return;
        }

        sb.Append("actions:").Append(NewLine);
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
            sb.Append("drillDownKeyPath: []").Append(NewLine);
            return;
        }

        sb.Append("drillDownKeyPath:").Append(NewLine);

        var i = 0;
        while (i < segments.Count)
        {
            var segment = segments[i];
            if (segment.Kind == KeyPathSegmentKind.Key && i + 1 < segments.Count && segments[i + 1].Kind == KeyPathSegmentKind.Index)
            {
                sb.Append("  - key: ").Append(StringUtility.QuoteString(segment.Value)).Append(NewLine);
                sb.Append("    index: ").Append(ExtractIndexLabel(segments[i + 1].Value)).Append(NewLine);
                i += 2;
                continue;
            }

            if (segment.Kind == KeyPathSegmentKind.Key)
            {
                sb.Append("  - key: ").Append(StringUtility.QuoteString(segment.Value)).Append(NewLine);
                i++;
                continue;
            }

            sb.Append("  - index: ").Append(ExtractIndexLabel(segment.Value)).Append(NewLine);
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
                sb.Append("  - type: Rename").Append(NewLine);
                sb.Append("    oldName: ").Append(StringUtility.QuoteString(rename.OldName)).Append(NewLine);
                sb.Append("    newName: ").Append(StringUtility.QuoteString(rename.NewName)).Append(NewLine);
                break;
            case DeleteColumnAction delete:
                sb.Append("  - type: Delete").Append(NewLine);
                sb.Append("    columnName: ").Append(StringUtility.QuoteString(delete.ColumnName)).Append(NewLine);
                break;
            case CastColumnAction cast:
                sb.Append("  - type: Cast").Append(NewLine);
                sb.Append("    columnName: ").Append(StringUtility.QuoteString(cast.ColumnName)).Append(NewLine);
                sb.Append(CultureInfo.InvariantCulture, $"    targetType: {cast.TargetType}").Append(NewLine);
                break;
            case FilterAction filter:
                sb.Append("  - type: Filter").Append(NewLine);
                sb.Append("    columnName: ").Append(StringUtility.QuoteString(filter.ColumnName)).Append(NewLine);
                sb.Append(CultureInfo.InvariantCulture, $"    operator: {filter.Operator}").Append(NewLine);
                sb.Append(CultureInfo.InvariantCulture, $"    comparisonType: {filter.ComparisonType}").Append(NewLine);
                sb.Append("    value: ").Append(StringUtility.QuoteString(filter.Value)).Append(NewLine);
                break;
            case FillColumnAction fill:
                sb.Append("  - type: Fill").Append(NewLine);
                sb.Append("    columnName: ").Append(StringUtility.QuoteString(fill.ColumnName)).Append(NewLine);
                sb.Append("    value: ").Append(StringUtility.QuoteString(fill.Value)).Append(NewLine);
                break;
            case FormatTimestampAction formatTimestamp:
                sb.Append("  - type: FormatTimestamp").Append(NewLine);
                sb.Append("    columnName: ").Append(StringUtility.QuoteString(formatTimestamp.ColumnName)).Append(NewLine);
                sb.Append("    targetFormat: ").Append(StringUtility.QuoteString(formatTimestamp.TargetFormat)).Append(NewLine);
                break;
            default:
                throw new UnreachableException("Unhandled MorphAction subtype in serializer");
        }
    }
}

using System.Text;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.App;

/// <summary>
/// Formats a KeyPath into the breadcrumb display string (e.g. <c>"data > orders[*]"</c>).
/// </summary>
internal static class KeyPathFormatter
{
    private const string Separator = " > ";

    /// <summary>
    /// Renders <paramref name="path"/> as a single breadcrumb line, collapsing every
    /// <see cref="KeyPathSegmentKind.Index"/> segment to <c>"[*]"</c> when
    /// <paramref name="collapseIndices"/> is <c>true</c> (Full Aggregation DrillDown semantics),
    /// or keeping the concrete index otherwise.
    /// </summary>
    /// <param name="path">The ordered path segments from root to the selected node.</param>
    /// <param name="collapseIndices">
    /// When <c>true</c>, every <see cref="KeyPathSegmentKind.Index"/> segment renders as <c>"[*]"</c>.
    /// </param>
    /// <returns>The formatted breadcrumb text. An empty path yields <c>"root"</c>.</returns>
    internal static string Format(IReadOnlyList<KeyPathSegment> path, bool collapseIndices)
    {
        if (path.Count == 0)
        {
            return "root";
        }

        StringBuilder builder = new();
        foreach (KeyPathSegment segment in path)
        {
            if (segment.Kind == KeyPathSegmentKind.Index)
            {
                builder.Append(collapseIndices ? "[*]" : segment.Value);
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(Separator);
            }

            builder.Append(segment.Value);
        }

        return builder.ToString();
    }
}

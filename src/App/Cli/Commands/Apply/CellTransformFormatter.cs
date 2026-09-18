using System.Diagnostics;
using System.Globalization;
using Refedle.App.Cli.IO;
using Refedle.Engine.Models;

namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// Applies a <see cref="CellTransformSpec"/> to an output cell. Cells that a timestamp
/// transform cannot reformat (non-value cells and unparseable text) pass through unchanged
/// so one bad cell cannot abort the whole batch run. Pure formatting only: which cell
/// presences are reportable data-quality issues is decided by the caller.
/// </summary>
internal static class CellTransformFormatter
{
    /// <summary>
    /// Transforms the cell according to the transform kind.
    /// </summary>
    /// <param name="transform">The transform to apply.</param>
    /// <param name="cell">The cell read from the source record.</param>
    /// <param name="result">The cell to write: the transformed cell on success, the original cell otherwise.</param>
    /// <param name="reason">Why the cell could not be transformed; <see cref="string.Empty"/> when it could.</param>
    /// <returns>
    /// <see langword="true"/> when the cell was processed as specified; <see langword="false"/>
    /// when the cell passes through unchanged because its value could not be transformed.
    /// </returns>
    public static bool TryFormat(CellTransformSpec transform, CellData cell, out CellData result, out string reason)
    {
        if (transform is FillSpec fill)
        {
            result = new CellData(fill.Value, CellPresence.Value, CellEncodingClassifier.Classify(fill.Value));
            reason = string.Empty;
            return true;
        }

        if (transform is TimestampFormatSpec fmt)
        {
            return TryApplyTimestampFormat(cell, fmt, out result, out reason);
        }

        throw new UnreachableException($"Unhandled CellTransformSpec: {transform.GetType().Name}");
    }

    private static bool TryApplyTimestampFormat(CellData cell, TimestampFormatSpec fmt, out CellData result, out string reason)
    {
        if (cell.Presence is not CellPresence.Value)
        {
            // Null/Missing/Invalid cells have no text to parse, so they pass through as-is.
            result = cell;
            reason = string.Empty;
            return true;
        }

        if (!DateTime.TryParse(cell.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            result = cell;
            reason = "not recognized as a timestamp";
            return false;
        }

        var formatted = parsed.ToString(fmt.TargetFormat, CultureInfo.InvariantCulture);
        result = new CellData(formatted, CellPresence.Value, CellEncodingClassifier.Classify(formatted));
        reason = string.Empty;
        return true;
    }
}

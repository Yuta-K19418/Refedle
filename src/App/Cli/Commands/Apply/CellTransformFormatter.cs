using System.Diagnostics;
using System.Globalization;
using Refedle.App.Cli.IO;
using Refedle.Engine.Models;

namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// Applies a <see cref="CellTransformSpec"/> to an output cell. Cells that a
/// timestamp transform cannot reformat (non-value cells and unparseable text)
/// pass through unchanged so one bad cell cannot abort the whole batch run.
/// </summary>
internal static class CellTransformFormatter
{
    /// <summary>
    /// Transforms the cell according to the transform kind.
    /// </summary>
    public static CellData Format(CellTransformSpec transform, CellData cell)
    {
        return transform switch
        {
            FillSpec fill => new CellData(fill.Value, CellPresence.Value, CellEncodingClassifier.Classify(fill.Value)),
            TimestampFormatSpec fmt => ApplyTimestampFormat(cell, fmt),
            _ => throw new UnreachableException($"Unhandled CellTransformSpec: {transform.GetType().Name}"),
        };
    }

    private static CellData ApplyTimestampFormat(CellData cell, TimestampFormatSpec fmt)
    {
        if (cell.Presence is not CellPresence.Value)
        {
            return cell;
        }

        if (!DateTime.TryParse(cell.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return cell;
        }

        var formatted = parsed.ToString(fmt.TargetFormat, CultureInfo.InvariantCulture);
        return new CellData(formatted, CellPresence.Value, CellEncodingClassifier.Classify(formatted));
    }
}

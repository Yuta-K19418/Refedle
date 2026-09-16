using Refedle.App.Cli.IO;
using Refedle.Engine.Models;

namespace Refedle.App.Cli.Commands.Apply;

internal static class CellResolver
{
    // Worded here, not in CellTransformFormatter: Invalid is a reader-layer signal, and which
    // presences count as reportable issues is CellResolver's policy.
    private const string InvalidCellReason = "unreadable source value";

    /// <summary>
    /// Derives the cell to write for one output column from the source cell and its transform.
    /// Pure: a non-empty <see cref="ResolvedCellData.Reason"/> marks a cell that passes through
    /// unprocessed, for the caller to record — this method records nothing itself. FillSpec's
    /// overwrite applies to an unreadable cell like any other (no source value consumed, so no
    /// issue); other transforms never receive unreadable cells — those pass through, reported.
    /// </summary>
    public static ResolvedCellData ResolveCell(CellTransformSpec? transform, CellData cell)
    {
        if (cell.Presence is CellPresence.Invalid && transform is not FillSpec)
        {
            return new ResolvedCellData(cell, InvalidCellReason);
        }

        if (transform is null)
        {
            return new ResolvedCellData(cell, string.Empty);
        }

        if (!CellTransformFormatter.TryFormat(transform, cell, out var formatted, out var reason))
        {
            return new ResolvedCellData(cell, reason);
        }

        return new ResolvedCellData(formatted, string.Empty);
    }
}

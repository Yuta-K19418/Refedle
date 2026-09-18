using Refedle.App.Cli.IO;

namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// A per-cell resolution outcome: the cell to write and, when it could not be processed as
/// the recipe specified, why. A ref struct so the span-backed <see cref="CellData"/> can be
/// carried without allocating.
/// </summary>
internal readonly ref struct ResolvedCellData(CellData cell, string reason)
{
    public CellData Cell { get; } = cell;

    public string Reason { get; } = reason;

    public bool HasIssue => Reason.Length > 0;
}

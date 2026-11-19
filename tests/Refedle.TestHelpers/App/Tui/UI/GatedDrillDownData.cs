using System.Text;
using Refedle.App.Tui;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.TestHelpers.App.Tui.UI;

/// <summary>Drill-down data whose row reads can be held mid-batch by a gate.</summary>
internal static class GatedDrillDownData
{
    /// <summary>
    /// DrillDown of 1000-row batches whose "name" values contain "Match" the given number of
    /// times per batch ("Other…" otherwise), wrapped in a row list whose read of the row at
    /// <paramref name="gateRowIndex"/> (the first row of the second batch by default) blocks
    /// until released — the worker is provably mid-flight while the gate is closed.
    /// </summary>
    internal static (DrillDownState DrillDown, GatedDrillDownRows Rows) CreateGatedDrillDown(
        int matchedInFirstBatch,
        int matchedInSecondBatch,
        int totalBatches,
        int gateRowIndex = 1000)
    {
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        IEnumerable<string> firstBatch = Enumerable.Range(0, matchedInFirstBatch).Select(static i => $"Match{i}")
            .Concat(Enumerable.Range(0, 1000 - matchedInFirstBatch).Select(static i => $"Other{i}"));
        IEnumerable<string> secondBatch = Enumerable.Range(0, matchedInSecondBatch).Select(static i => $"Match{1000 + i}")
            .Concat(Enumerable.Range(0, 1000 - matchedInSecondBatch).Select(static i => $"Other{1000 + i}"));
        IEnumerable<string> laterBatches = Enumerable.Range(2000, (totalBatches - 2) * 1000).Select(static i => $"Other{i}");
        FocusedTableRow[] rows = [.. firstBatch
            .Concat(secondBatch)
            .Concat(laterBatches)
            .Select(static (name, index) => new FocusedTableRow(Encoding.UTF8.GetBytes($"{{\"name\":\"{name}\"}}"), $"[{index}]"))];
        GatedDrillDownRows gated = new(rows, gateRowIndex);
        return (new DrillDownState(gated, schema, ViewMode.JsonArrayTree, KeyPath: [], ActionStack: []), gated);
    }
}

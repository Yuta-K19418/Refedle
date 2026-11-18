using System.Text;
using Refedle.App.Tui;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.IntegrationTests.App.Tui.UI.ViewManagerSupport;

/// <summary>Schemas, filters, indexers and drill-down builders shared by the ViewManager test partials.</summary>
internal static class ViewManagerTestData
{
    internal static TableSchema CreateItemCountSchema(DataFormat format) => new()
    {
        SourceFormat = format,
        Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }],
    };

    internal static FilterAction CreateColumnFilter(string value) =>
        FilterAction.Create("col1", FilterOperator.Equals, ComparisonType.Text, value).Value;

    internal static DataRowIndexer BuildCsvIndexer(string filePath)
    {
        DataRowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        return indexer;
    }

    internal static TableSchema CreateJsonLinesSchema(params string[] columnNames) =>
        new()
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [.. columnNames.Select((name, index) => new ColumnSchema { Name = name, Type = ColumnType.Text, ColumnIndex = index })],
        };

    internal static DrillDownState CreateNumberedDrillDown(int rowCount)
    {
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        FocusedTableRow[] rows = [.. Enumerable.Range(0, rowCount).Select(index => new FocusedTableRow(Encoding.UTF8.GetBytes($"{{\"name\":\"Alice{index}\"}}"), $"[{index}]"))];
        return new DrillDownState(rows, schema, ViewMode.JsonArrayTree, KeyPath: [], ActionStack: []);
    }

    /// <summary>
    /// DrillDown with 1250 rows (two indexer batches of 1000 + 250) whose "name" values
    /// contain "Match" exactly <paramref name="matchedInFirstBatch"/> /
    /// <paramref name="matchedInSecondBatch"/> times per batch, for threshold-boundary tests.
    /// </summary>
    internal static DrillDownState CreateMixedDrillDown(int matchedInFirstBatch, int matchedInSecondBatch)
    {
        TableSchema schema = new()
        {
            SourceFormat = DataFormat.JsonArray,
            Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text }],
        };
        IEnumerable<string> names = Enumerable.Range(0, matchedInFirstBatch).Select(static i => $"Match{i}")
            .Concat(Enumerable.Range(0, 1000 - matchedInFirstBatch).Select(static i => $"Other{i}"))
            .Concat(Enumerable.Range(0, matchedInSecondBatch).Select(static i => $"Match{1000 + i}"))
            .Concat(Enumerable.Range(0, 250 - matchedInSecondBatch).Select(static i => $"Other{1000 + i}"));
        FocusedTableRow[] rows = [.. names.Select(static (name, index) => new FocusedTableRow(Encoding.UTF8.GetBytes($"{{\"name\":\"{name}\"}}"), $"[{index}]"))];
        return new DrillDownState(rows, schema, ViewMode.JsonArrayTree, KeyPath: [], ActionStack: []);
    }

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

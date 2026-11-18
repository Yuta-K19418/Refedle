using Refedle.App.Tui.UI.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

internal sealed class ThreeColumnNumberedTableSource(long totalRows) : IExtendedTableSource
{
    public long TotalRows { get; set; } = totalRows;
    public int Columns => 3;
    public string[] ColumnNames => ["a", "b", "c"];
    public string[] RawColumnNames => ["a", "b", "c"];
    public object this[long row, int col] => $"row{row}";
}

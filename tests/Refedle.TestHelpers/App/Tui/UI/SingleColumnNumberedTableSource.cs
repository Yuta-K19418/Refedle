using Refedle.App.Tui.UI.Views;

namespace Refedle.TestHelpers.App.Tui.UI;

internal sealed class SingleColumnNumberedTableSource(long totalRows) : IExtendedTableSource
{
    public long TotalRows => totalRows;
    public int Columns => 1;
    public string[] ColumnNames => ["a"];
    public string[] RawColumnNames => ["a"];
    public object this[long row, int col] => $"row{row}";
}

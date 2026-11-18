using Refedle.App.Tui.UI.Views;

namespace Refedle.Tests.App.Tui.UI;

internal sealed class StringRowsFakeTableSource(string[][] data, string[] columnNames) : IExtendedTableSource
{
    public long TotalRows => data.Length;
    public int Columns => columnNames.Length;
    public string[] ColumnNames => columnNames;
    public string[] RawColumnNames => columnNames;
    public object this[long row, int col] => data[(int)row][col];
}

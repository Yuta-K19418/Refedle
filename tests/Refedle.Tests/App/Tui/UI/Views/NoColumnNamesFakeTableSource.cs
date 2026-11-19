using Refedle.App.Tui.UI.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

internal sealed class NoColumnNamesFakeTableSource(string[][] data) : IExtendedTableSource
{
    public long TotalRows => data.Length;
    public int Columns => data.Length > 0 ? data[0].Length : 0;
    public string[] ColumnNames => [];
    public string[] RawColumnNames => [];
    public object this[long row, int col] => data[(int)row][col];
}

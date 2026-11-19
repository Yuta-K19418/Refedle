using Refedle.App.Tui.UI.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

internal sealed class ObjectRowsFakeTableSource(string[] columnNames, IReadOnlyList<IReadOnlyList<object>> rows) : IExtendedTableSource
{
    public long TotalRows => rows.Count;
    public int Columns => columnNames.Length;
    public string[] ColumnNames => columnNames;
    public string[] RawColumnNames => columnNames;
    public object this[long row, int col] => rows[(int)row][col];
}

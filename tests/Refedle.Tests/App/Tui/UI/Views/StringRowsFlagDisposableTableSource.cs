using Refedle.App.Tui.UI.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

internal sealed class StringRowsFlagDisposableTableSource(string[][] data, string[] columnNames)
    : IExtendedTableSource, IDisposable
{
    public bool IsDisposed { get; private set; }
    public long TotalRows => data.Length;
    public int Columns => columnNames.Length;
    public string[] ColumnNames => columnNames;
    public string[] RawColumnNames => columnNames;
    public object this[long row, int col] => data[(int)row][col];
    public void Dispose() => IsDisposed = true;
}

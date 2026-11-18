using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

internal sealed class FlagDisposableBasicTableSource : ITableSource, IDisposable
{
    public bool IsDisposed { get; private set; }
    public int Rows => 0;
    public int Columns => 0;
    public string[] ColumnNames => [];
    public object this[int row, int col] => throw new NotImplementedException();
    public void Dispose() => IsDisposed = true;
}

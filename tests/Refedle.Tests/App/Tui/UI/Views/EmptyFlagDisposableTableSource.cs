using Refedle.App.Tui.UI.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

internal sealed class EmptyFlagDisposableTableSource : IExtendedTableSource, IDisposable
{
    public bool IsDisposed { get; private set; }
    public long TotalRows => 0;
    public int Columns => 0;
    public string[] ColumnNames => [];
    public string[] RawColumnNames => [];
    public object this[long row, int col] => throw new NotImplementedException();
    public void Dispose() => IsDisposed = true;
}

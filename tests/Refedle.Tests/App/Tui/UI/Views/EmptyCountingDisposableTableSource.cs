using Refedle.App.Tui.UI.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

internal sealed class EmptyCountingDisposableTableSource : IExtendedTableSource, IDisposable
{
    public bool IsDisposed => DisposeCount > 0;
    public int DisposeCount { get; private set; }
    public long TotalRows => 0;
    public int Columns => 0;
    public string[] ColumnNames => [];
    public string[] RawColumnNames => [];
    public object this[long row, int col] => throw new NotImplementedException();
    public void Dispose() => DisposeCount++;
}

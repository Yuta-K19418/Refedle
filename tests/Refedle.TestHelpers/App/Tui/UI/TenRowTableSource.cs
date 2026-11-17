using Refedle.App.Tui.UI.Views;

namespace Refedle.TestHelpers.App.Tui.UI;

internal sealed class TenRowTableSource(string[] columnNames, string[] rawColumnNames) : IExtendedTableSource
{
    public long TotalRows => 10;
    public int Columns => columnNames.Length;
    public string[] ColumnNames => columnNames;
    public string[] RawColumnNames => rawColumnNames;
    public object this[long row, int col] => $"R{row}C{col}";

    public static void AddColumn(string _) { }
    public static void AddRow() { }
    public static void RemoveColumn(int _) { }
    public static void RemoveRow(int _) { }
    public static void Clear() { }
}

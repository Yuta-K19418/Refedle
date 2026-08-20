using System.Text;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.Json;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Terminal.Gui.Views;

namespace Refedle.App.Views;

/// <summary>
/// ITableSource backed by pre-materialized <see cref="FocusedTableRow"/> rows.
/// </summary>
internal sealed class FocusedTableSource : ITableSource
{
    private readonly IReadOnlyList<FocusedTableRow> _rows;
    private readonly TableSchema _schema;
    private readonly string[] _columnNames;
    private readonly string[] _rawColumnNames;
    private readonly byte[][] _columnNamesUtf8;

    internal FocusedTableSource(DrillDownState drillDown)
    {
        ArgumentNullException.ThrowIfNull(drillDown);
        _rows = drillDown.Rows;
        _schema = drillDown.Schema;
        _columnNames = ["#", .. drillDown.Schema.Columns.Select(c => $"{c.Name} ({ColumnTypeLabel.ToLabel(c.Type)})")];
        _rawColumnNames = ["#", .. drillDown.Schema.Columns.Select(c => c.Name)];
        _columnNamesUtf8 = [.. drillDown.Schema.Columns.Select(c => Encoding.UTF8.GetBytes(c.Name))];
    }

    /// <inheritdoc/>
    public int Rows => _rows.Count;

    /// <inheritdoc/>
    public int Columns => _schema.ColumnCount + 1;

    /// <inheritdoc/>
    public string[] ColumnNames => _columnNames;

    /// <summary>
    /// Gets the raw (unlabeled) column names in output order, with <c>"#"</c> at index 0.
    /// Use these when constructing <see cref="MorphAction"/>s so that action
    /// <c>ColumnName</c> values match the DrillDown schema names.
    /// </summary>
    internal string[] RawColumnNames => _rawColumnNames;

    /// <inheritdoc/>
    public object this[int row, int col]
    {
        get
        {
            if (row < 0 || row >= Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            if (col < 0 || col >= Columns)
            {
                throw new ArgumentOutOfRangeException(nameof(col));
            }

            if (col == 0)
            {
                return _rows[row].HashValue;
            }

            return JsonObjectCellExtractor.ExtractCell(_rows[row].Bytes.Span, _columnNamesUtf8[col - 1]);
        }
    }
}

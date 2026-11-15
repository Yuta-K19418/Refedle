using Refedle.Engine.IO;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models;

namespace Refedle.App.Tui.UI.Views;

/// <summary>
/// Provides virtual table data source for Terminal.Gui's TableView.
/// Delegates to DataRowCache for efficient row retrieval.
/// </summary>
internal sealed class VirtualTableSource : IExtendedTableSource, IDisposable
{
    private readonly DataRowCache _cache;
    private readonly TableSchema _schema;
    private readonly string[] _columnNames;
    private readonly string[] _rawColumnNames;
    private readonly Action<string> _onReadFailed;
    private bool _readFailed;
    private bool _disposed;

    /// <param name="indexer">The completed or in-progress row indexer of the CSV file.</param>
    /// <param name="schema">The table schema of the CSV file.</param>
    /// <param name="onReadFailed">
    /// Called at most once, while the table is being drawn, when a row window cannot be read
    /// because a row does not have the header's column count. Afterwards every cell is empty.
    /// It must not replace views synchronously.
    /// </param>
    public VirtualTableSource(IRowIndexer indexer, TableSchema schema, Action<string> onReadFailed)
    {
        ArgumentNullException.ThrowIfNull(onReadFailed);
        _schema = schema;
        _columnNames = [.. _schema.Columns.Select(static c => $"{c.Name} ({ColumnTypeLabel.ToLabel(c.Type)})")];
        _rawColumnNames = [.. _schema.Columns.Select(static c => c.Name)];
        _onReadFailed = onReadFailed;
        _cache = new DataRowCache(indexer, _schema.ColumnCount);
    }

    public long TotalRows => _cache.TotalRows;
    public int Columns => _schema.ColumnCount;
    public string[] ColumnNames => _columnNames;
    /// <inheritdoc/>
    public string[] RawColumnNames => _rawColumnNames;

    public object this[long row, int col]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (row < 0 || row >= TotalRows)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            if (col < 0 || col >= Columns)
            {
                throw new ArgumentOutOfRangeException(nameof(col));
            }

            if (_readFailed)
            {
                return string.Empty;
            }

            var rowData = ReadRow(row);

            if (col < rowData.Count)
            {
                var memory = rowData[col];
                return memory.IsEmpty ? string.Empty : new string(memory.Span);
            }

            // Return empty string for columns that might not exist in a ragged CSV row
            return string.Empty;
        }
    }

    private CsvDataRow ReadRow(long row)
    {
        try
        {
            return _cache.GetRow(row);
        }
        catch (InvalidDataException ex)
        {
            _readFailed = true;
            _onReadFailed(ex.Message);
            return [];
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cache.Dispose();
        _disposed = true;
    }
}

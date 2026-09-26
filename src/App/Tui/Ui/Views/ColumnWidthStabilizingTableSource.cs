using System.Globalization;
using Terminal.Gui.Text;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views;

/// <summary>
/// Decorates an <see cref="ITableSource"/> to track the maximum cell width ever observed per
/// column and pins it as the column's <see cref="ColumnStyle.MinWidth"/>.
/// </summary>
/// <remarks>
/// TableView recalculates column widths from only the currently visible rows on every redraw,
/// so scrolling to rows with shorter values shrinks columns and produces flicker. Since
/// <see cref="ColumnStyle.MinWidth"/> is enforced as a floor by TableView's own width calculation,
/// keeping it pinned to the largest width ever seen for that column stabilizes the layout without
/// otherwise altering how cells are rendered.
/// <para>
/// The wrapped source's column count can grow after construction (e.g. <c>JsonLinesTableSource</c>
/// widens its schema as more rows are scanned in the background), so tracked state is grown lazily
/// under <see cref="_lock"/> rather than sized once up front.
/// </para>
/// </remarks>
internal sealed class ColumnWidthStabilizingTableSource : ITableSource, IDisposable
{
    private readonly ITableSource _inner;
    private readonly TableStyle _style;
    private readonly Lock _lock = new();
    private readonly List<int> _maxObservedWidths = [];
    private readonly List<ColumnStyle> _columnStyles = [];
    private bool _disposed;

    internal ColumnWidthStabilizingTableSource(ITableSource inner, TableStyle style)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(style);

        _inner = inner;
        _style = style;

        lock (_lock)
        {
            EnsureColumnsTracked(inner.Columns);
        }
    }

    /// <summary>The decorated source, exposed so callers can recover the underlying source type.</summary>
    internal ITableSource Inner => _inner;

    public int Rows => _inner.Rows;
    public int Columns => _inner.Columns;
    public string[] ColumnNames => _inner.ColumnNames;

    public object this[int row, int col]
    {
        get
        {
            var value = _inner[row, col];
            TrackWidth(col, value);
            return value;
        }
    }

    /// <summary>Disposes the wrapped source when it implements <see cref="IDisposable"/>. This decorator owns it.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        if (_inner is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void TrackWidth(int col, object value)
    {
        var representation = value is string text ? text : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        var width = representation.GetColumns();

        lock (_lock)
        {
            EnsureColumnsTracked(_inner.Columns);

            var columnStyle = _columnStyles[col];
            var clampedWidth = Math.Min(width, columnStyle.MaxWidth);

            if (clampedWidth <= _maxObservedWidths[col])
            {
                return;
            }

            _maxObservedWidths[col] = clampedWidth;
            columnStyle.MinWidth = clampedWidth;
        }
    }

    /// <summary>Grows the tracked width/style state to cover <paramref name="columnCount"/> columns. Callers must hold <see cref="_lock"/>.</summary>
    private void EnsureColumnsTracked(int columnCount)
    {
        for (var col = _maxObservedWidths.Count; col < columnCount; col++)
        {
            var columnStyle = _style.GetOrCreateColumnStyle(col);
            var headerWidth = Math.Min(_inner.ColumnNames[col].GetColumns(), columnStyle.MaxWidth);
            var initialWidth = Math.Max(headerWidth, columnStyle.MinWidth);
            columnStyle.MinWidth = initialWidth;

            _maxObservedWidths.Add(initialWidth);
            _columnStyles.Add(columnStyle);
        }
    }
}

using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI.Views;

/// <summary>
/// The paging layer placed between TableView and an <see cref="IExtendedTableSource"/>,
/// and the only <see cref="ITableSource"/> implementation in the repository.
/// Exposes the pages surrounding the current page (previous, current, and next — two
/// pages at the start and end of the table) so tables with more than
/// <see cref="int.MaxValue"/> rows fit Terminal.Gui's int-based row addressing.
/// </summary>
/// <remarks>
/// Row mapping: the absolute row is <c>(firstVisiblePage - 1) * pageSize + displayedRow</c>,
/// where <c>firstVisiblePage = max(1, pageNumber - 1)</c> (the same calculation as <see cref="WindowStart"/>).
/// <see cref="MorphTableView"/> detects selections that cross into a neighboring page,
/// calls <see cref="SwitchPageFor"/>, and corrects the selection row and scroll offset
/// by the window shift, so page boundaries stay invisible to the user.
/// Not thread-safe by design; all members are expected to run on the UI thread.
/// </remarks>
internal sealed class PagedTableSource : ITableSource, IDisposable
{
    /// <summary>
    /// The default page size. Three pages must fit within <see cref="int.MaxValue"/>,
    /// so this is the largest size <see cref="PagedTableSource"/> accepts.
    /// </summary>
    internal const int DefaultPageSize = int.MaxValue / 3;

    private readonly IExtendedTableSource _inner;
    private readonly int _pageSize;
    private long _pageNumber = 1;
    private bool _disposed;

    internal PagedTableSource(IExtendedTableSource inner, int pageSize = DefaultPageSize)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, int.MaxValue / 3);

        _inner = inner;
        _pageSize = pageSize;
    }

    /// <summary>The decorated source, exposed so callers can recover the underlying source.</summary>
    internal IExtendedTableSource Inner => _inner;

    /// <summary>Number of rows per page.</summary>
    internal int PageSize => _pageSize;

    /// <summary>Total row count of the decorated source.</summary>
    internal long TotalRows => _inner.TotalRows;

    /// <summary>Absolute row index displayed at row 0 of this <see cref="ITableSource"/>.</summary>
    internal long WindowStart => (FirstPageNumber - 1) * _pageSize;

    /// <summary>
    /// The 1-based page the displayed window is centered on. Assign to switch pages;
    /// the value is clamped to the valid page range.
    /// </summary>
    internal long CurrentPageNumber
    {
        get => _pageNumber;
        set => _pageNumber = Math.Clamp(value, 1L, Math.Max(1L, PageCount));
    }

    /// <summary>Gets the number of displayed rows: the visible pages, capped at the remaining rows.</summary>
    public int Rows
    {
        get
        {
            var windowSpan = (LastPageNumber - FirstPageNumber + 1) * _pageSize;
            var remaining = _inner.TotalRows - WindowStart;
            return (int)Math.Min(remaining, windowSpan);
        }
    }

    /// <inheritdoc/>
    public int Columns => _inner.Columns;

    /// <inheritdoc/>
    public string[] ColumnNames => _inner.ColumnNames;

    /// <inheritdoc/>
    public object this[int row, int col] => _inner[WindowStart + row, col];

    /// <summary>
    /// Switches <see cref="CurrentPageNumber"/> to the page containing the given absolute row
    /// and returns the resulting shift of <see cref="WindowStart"/> (positive when the window
    /// moved forward). A return value of <c>0</c> means the displayed rows did not change.
    /// </summary>
    internal long SwitchPageFor(long absoluteRow)
    {
        var previousWindowStart = WindowStart;
        CurrentPageNumber = Math.Clamp(absoluteRow / _pageSize + 1, 1L, Math.Max(1L, PageCount));
        return WindowStart - previousWindowStart;
    }

    // Page numbers stay long: with small page sizes the count can exceed int.MaxValue.
    private long PageCount => _inner.TotalRows == 0 ? 0 : ((_inner.TotalRows - 1) / _pageSize) + 1;

    private long FirstPageNumber => Math.Max(1L, _pageNumber - 1);

    // Compared before adding 1 so the last page number cannot overflow long.
    private long LastPageNumber => _pageNumber >= PageCount ? Math.Max(1L, PageCount) : _pageNumber + 1;

    /// <summary>Disposes the wrapped source when it implements <see cref="IDisposable"/>. This decorator owns it.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_inner is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

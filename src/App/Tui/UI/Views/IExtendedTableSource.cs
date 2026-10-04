using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI.Views;

/// <summary>
/// The repository's standard table source contract, addressing rows as <see cref="long"/>
/// so sources with more than <see cref="int.MaxValue"/> rows can be served.
/// Terminal.Gui's <see cref="ITableSource"/> is int-based; it is implemented only by
/// <see cref="PagedTableSource"/>, the paging layer placed between TableView and the
/// sources behind this interface. Implementations know nothing about paging.
/// </summary>
internal interface IExtendedTableSource
{
    /// <summary>
    /// Gets the total number of rows in the source.
    /// </summary>
    /// <remarks>
    /// Implementations whose row count grows after construction (e.g. while a background
    /// index build is still running) must return the count current at the moment of each call.
    /// </remarks>
    long TotalRows { get; }

    /// <summary>
    /// Gets the number of columns in the source.
    /// </summary>
    int Columns { get; }

    /// <summary>
    /// Gets the labeled column names in output order, aligned index-for-index with
    /// <see cref="RawColumnNames"/>.
    /// </summary>
    /// <remarks>
    /// Implementations whose column set can grow after construction (e.g.
    /// <see cref="JsonLinesTableSource"/>, whose schema widens as the background scan
    /// discovers new columns) must return the column names current at the moment of each call.
    /// </remarks>
    string[] ColumnNames { get; }

    /// <summary>
    /// Gets the raw (unlabeled) column names in output order, aligned index-for-index
    /// with <see cref="ColumnNames"/>. Use these when the caller must
    /// resolve a column index to the schema name (e.g. constructing morph actions),
    /// where the type-label suffix of <see cref="ColumnNames"/> is unwanted.
    /// </summary>
    string[] RawColumnNames { get; }

    /// <summary>
    /// Gets the cell value at the specified zero-based row and column index.
    /// </summary>
    object this[long row, int col] { get; }
}

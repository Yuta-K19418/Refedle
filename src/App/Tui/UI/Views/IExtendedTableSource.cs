using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI.Views;

/// <summary>
/// The repository's standard table source contract, extending Terminal.Gui's
/// <see cref="ITableSource"/> with members the repository's own views need.
/// Additional members will be added over time; every table source passed to a
/// repository API must implement this interface rather than the bare
/// <see cref="ITableSource"/>.
/// </summary>
internal interface IExtendedTableSource : ITableSource
{
    /// <summary>
    /// Gets the raw (unlabeled) column names in output order, aligned index-for-index
    /// with <see cref="ITableSource.ColumnNames"/>. Use these when the caller must
    /// resolve a column index to the schema name (e.g. constructing morph actions),
    /// where the type-label suffix of <see cref="ITableSource.ColumnNames"/> is unwanted.
    /// </summary>
    /// <remarks>
    /// Implementations whose column set can grow after construction (e.g.
    /// <see cref="JsonLinesTableSource"/>, whose schema widens as the background scan
    /// discovers new columns) must return the column names current at the moment of each call.
    /// </remarks>
    string[] RawColumnNames { get; }
}

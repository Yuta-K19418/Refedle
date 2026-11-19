using System.Drawing;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models;
using Terminal.Gui.ViewBase;

namespace Refedle.App.Tui.UI;

/// <summary>
/// Rebuilds the table source of a JSON Lines table view once the schema has been refined.
/// </summary>
/// <param name="state">The application state that holds the Action Stack.</param>
/// <param name="getCurrentView">Returns the content view currently shown.</param>
/// <remarks>
/// Threading contract: <see cref="Rebuild"/> must be called on the UI thread. It is reached from the
/// schema-refined callback, which <c>BackgroundSchemaRefiner</c> invokes inside its
/// <c>uiThreadInvoke</c> dispatcher.
/// </remarks>
internal sealed class LazyTransformerRebuilder(AppState state, Func<View?> getCurrentView)
{
    /// <summary>
    /// Replaces only the table source of <paramref name="view"/> with a <see cref="LazyTransformer"/>
    /// built from the refined schema, so columns discovered after indexing become visible.
    /// The existing filter row indexer is reused, and the page and selection are carried over.
    /// </summary>
    internal void Rebuild(
        JsonLinesTableView view,
        JsonLinesTableSource source,
        LazyTransformer current,
        TableSchema refined)
    {
        if (getCurrentView() != view || view.Table is not PagedTableSource oldPaged)
        {
            return;
        }

        long pageNumber = oldPaged.CurrentPageNumber;
        Point? selection = view.Value?.SelectedCell;
        int rowOffset = view.RowOffset;
        int columnOffset = view.ColumnOffset;

        Func<IReadOnlyList<FilterSpec>, IFilterRowIndexer>? reuseFilterIndexer =
            current.FilterRowIndexer is { } filterIndexer ? _ => filterIndexer : null;
        IExtendedTableSource rebuilt = LazyTransformer.Create(source, refined, state.ActionStack, reuseFilterIndexer);
        view.Table = new PagedTableSource(new ColumnWidthStabilizingTableSource(rebuilt, view.Style));
        PagedTableSource newPaged = (PagedTableSource)view.Table;
        newPaged.CurrentPageNumber = pageNumber;

        if (selection is { } cell)
        {
            view.SetSelection(Math.Min(cell.X, newPaged.Columns - 1), cell.Y, false);
            view.RowOffset = rowOffset;
            view.ColumnOffset = columnOffset;
            view.Update();
        }

        view.SetNeedsDraw();
    }
}

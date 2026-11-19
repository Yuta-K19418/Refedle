using System.Text;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.IO;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI;

/// <summary>
/// A constructed table view together with the (possibly transformed) source it displays.
/// </summary>
/// <param name="View">The table view.</param>
/// <param name="Source">The source behind the view, before paging and width stabilizing.</param>
internal readonly record struct TableViewResult(MorphTableView View, IExtendedTableSource Source);

/// <summary>
/// Builds the table sources and views for CSV, JSON Lines and FocusedTable modes.
/// </summary>
/// <param name="state">The application state that holds the Action Stack and session token.</param>
/// <param name="uiThreadInvoke">Dispatches an action onto the UI thread.</param>
/// <param name="getCurrentView">Returns the content view currently shown.</param>
/// <param name="rebuilder">Rebuilds a JSON Lines table source once the schema is refined.</param>
/// <param name="onMorphAction">Handles a morph action raised by a table view.</param>
/// <param name="onRowReadError">Handles a CSV row read error with the erroring view and its session token.</param>
/// <remarks>
/// Threading contract: every <c>Create*</c> method must be called on the UI thread. Indexer
/// notifications (<see cref="IRowIndexer.FirstCheckpointReached"/>, which can fire on a worker
/// thread, and the CSV row read error, which is raised while the table draws) are re-entered
/// through <paramref name="uiThreadInvoke"/> before any view or state is touched.
/// </remarks>
internal sealed class TableViewFactory(
    AppState state,
    Action<Action> uiThreadInvoke,
    Func<View?> getCurrentView,
    LazyTransformerRebuilder rebuilder,
    Action<IMorphAction> onMorphAction,
    Action<View?, string, CancellationToken> onRowReadError)
{
    /// <summary>
    /// Matched rows the filter scan must produce before the table view replaces the loading
    /// view; passed into the indexer so its threshold wait fires at the same count.
    /// </summary>
    private const int FilterScanInitialTableRows = 200;

    /// <summary>
    /// Creates the virtualized CSV table view, wrapping the source with <see cref="LazyTransformer"/>
    /// when the Action Stack is non-empty.
    /// </summary>
    internal TableViewResult CreateCsvTable(IRowIndexer indexer, TableSchema schema)
    {
        // Assigned below; the queued error callback compares it with the then-current view.
        // The token marks the callback stale once another file session has started.
        CsvTableView? view = null;
        CancellationToken sessionToken = state.Cts.Token;
        IExtendedTableSource rawSource = new VirtualTableSource(
            indexer,
            schema,
            message => uiThreadInvoke(() => onRowReadError(view, message, sessionToken)));
        IExtendedTableSource source = state.ActionStack.Count > 0
            ? LazyTransformer.Create(
                rawSource,
                schema,
                state.ActionStack,
                filterSpecs => new Refedle.Engine.IO.Csv.FilterRowIndexer(
                    indexer,
                    schema.Columns.Count,
                    filterSpecs
                )
            )
            : rawSource;

        TableStyle style = new() { AlwaysShowHeaders = true };
        view = new CsvTableView
        {
            Table = new PagedTableSource(new ColumnWidthStabilizingTableSource(source, style)),
            Style = style,
            OnMorphAction = onMorphAction,
            IsRowIndexComplete = () => indexer.IsIndexingCompleted,
        };
        SetInitialSelectionWhenReady(view, indexer);
        return new TableViewResult(view, source);
    }

    /// <summary>
    /// Creates the JSON Lines table view, wrapping the source with <see cref="LazyTransformer"/>
    /// when the Action Stack is non-empty. Registers <see cref="AppState.OnSchemaRefined"/> for
    /// background schema updates.
    /// </summary>
    internal TableViewResult CreateJsonLinesTable(IRowIndexer indexer, TableSchema schema)
    {
        RowByteCache cache = new(indexer);
        JsonLinesTableSource source = new(cache, schema);

        IExtendedTableSource tableSource = state.ActionStack.Count > 0
            ? LazyTransformer.Create(
                source,
                schema,
                state.ActionStack,
                filterSpecs => new Refedle.Engine.IO.JsonLines.FilterRowIndexer(
                    indexer,
                    indexer.FilePath,
                    [.. schema.Columns.Select(static c => Encoding.UTF8.GetBytes(c.Name))],
                    filterSpecs
                )
            )
            : source;

        TableStyle style = new() { AlwaysShowHeaders = true };
        JsonLinesTableView view = new()
        {
            Table = new PagedTableSource(new ColumnWidthStabilizingTableSource(tableSource, style)),
            Style = style,
            OnMorphAction = onMorphAction,
            IsRowIndexComplete = () => indexer.IsIndexingCompleted,
        };
        state.SetSchemaRefinedCallback(refined =>
        {
            source.UpdateSchema(refined);
            if (tableSource is LazyTransformer lazy)
            {
                rebuilder.Rebuild(view, source, lazy, refined);
            }
        });
        SetInitialSelectionWhenReady(view, indexer);
        return new TableViewResult(view, tableSource);
    }

    /// <summary>
    /// Creates the FocusedTable source, wrapping it with <see cref="FocusedTableTransformer"/>
    /// when the DrillDown Action Stack is non-empty.
    /// </summary>
    internal static IExtendedTableSource CreateFocusedSource(DrillDownState drillDown)
    {
        IExtendedTableSource rawSource = new FocusedTableSource(drillDown);
        return drillDown.ActionStack.Count > 0
            ? FocusedTableTransformer.Create(
                rawSource,
                drillDown.Schema,
                drillDown.ActionStack,
                FilterScanInitialTableRows)
            : rawSource;
    }

    /// <summary>
    /// Creates the FocusedTable view over <paramref name="source"/>.
    /// </summary>
    internal FocusedTableView CreateFocusedTableView(IExtendedTableSource source, TableStyle style)
    {
        FocusedTableView view = new()
        {
            Table = new PagedTableSource(new ColumnWidthStabilizingTableSource(source, style)),
            Style = style,
            OnMorphAction = onMorphAction,
        };
        return view;
    }

    private void SetInitialSelectionWhenReady(MorphTableView view, IRowIndexer indexer)
    {
        if (indexer.TotalRows > 0)
        {
            view.SetSelection(0, 0, false);
            view.Update();
            return;
        }

        void onReady()
        {
            // Unsubscribe immediately to ensure initial selection logic runs only once
            // and to release the captured view reference for garbage collection.
            indexer.FirstCheckpointReached -= onReady;

            uiThreadInvoke(() =>
            {
                // Only set if this view is still active and the user hasn't moved the cursor yet
                if (getCurrentView() == view && view.Table is not null && view.Table.Rows > 0
                    && (view.Value is null || view.Value.SelectedCell.Y <= 0))
                {
                    view.SetSelection(0, 0, false);
                    view.Update();
                    view.SetNeedsDraw();
                }
            });
        }

        indexer.FirstCheckpointReached += onReady;
    }
}

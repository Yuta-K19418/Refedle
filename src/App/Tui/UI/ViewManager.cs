using System.Diagnostics;
using System.Globalization;
using System.Text;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Key = Terminal.Gui.Input.Key;

namespace Refedle.App.Tui.UI;

/// <summary>
/// Manages the active content view inside a Terminal.Gui <see cref="Window"/>.
/// Reads Engine-layer objects from <see cref="AppState"/> and switches the visible view accordingly.
/// Has no dependency on the Engine layer directly.
/// </summary>
/// <remarks>
/// Threading contract: all ViewManager state — including the filter-scan session state
/// owned by the <see cref="FilterScanManager"/> (the session, its staged table view and
/// the session CTS cancel/dispose) — is UI-thread exclusive. Every synchronous entry
/// point (<see cref="SwitchToFocusedTable"/>, the other
/// view switches, <see cref="Dispose"/>, …) must be called on the UI thread.
/// The only exceptions are the asynchronous entry points (for example
/// <see cref="FullAggregationDrillDownAsync"/>, reached from recipe replay after
/// <c>ConfigureAwait(false)</c>): they may be called off the UI thread, run their background
/// work there, and apply every state and view change inside an <c>_uiThreadInvoke</c> callback.
/// <c>_uiThreadInvoke</c> is a dispatcher that must execute the given action on the UI thread;
/// it is also how worker-thread notifications re-enter, and it does not relax the contract for
/// synchronous entry points.
/// </remarks>
internal sealed class ViewManager : IDisposable
{
    /// <summary>
    /// Matched rows the filter scan must produce before the table view replaces the loading
    /// view; passed into the indexer so its threshold wait fires at the same count.
    /// </summary>
    private const int FilterScanInitialTableRows = 200;

    private readonly Window _container;
    private readonly AppState _state;
    private readonly ModeController _modeController;
    private readonly IFilterScanRunner _lazyFilterScanRunner;
    private readonly Action<Action> _uiThreadInvoke;
    private readonly BreadcrumbBar _breadcrumbBar;
    private readonly View _contentContainer;
    private readonly FilterScanManager _filterScanManager;
    private View? _currentView;
    private View? _lazyScanView;
    private Label? _itemCountLabel;
    private bool _disposed;

    internal ViewManager(
        Window container,
        AppState state,
        ModeController modeController,
        IFilterScanRunner filterScanRunner,
        Action<Action> uiThreadInvoke)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(modeController);
        ArgumentNullException.ThrowIfNull(filterScanRunner);
        ArgumentNullException.ThrowIfNull(uiThreadInvoke);
        _container = container;
        _state = state;
        _modeController = modeController;
        _lazyFilterScanRunner = filterScanRunner;
        _uiThreadInvoke = uiThreadInvoke;
        // Narrow delegates keep FilterScanManager decoupled from ViewManager itself while
        // it drives the content view, status bar and error display on the UI thread.
        _filterScanManager = new FilterScanManager(
            filterScanRunner,
            uiThreadInvoke,
            () => state.Cts.Token,
            CreateFocusedTableView,
            SwapView,
            RefreshStatusBarHints,
            ShowError,
            GetCurrentView);
        _breadcrumbBar = new BreadcrumbBar();
        _contentContainer = new View
        {
            X = 0,
            Y = Pos.Bottom(_breadcrumbBar),
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1, // Leave room for StatusBar at the bottom
            CanFocus = true,
        };
        _container.Add(_breadcrumbBar, _contentContainer);
    }

    /// <summary>
    /// Refreshes the status bar hints based on the current application state.
    /// When indexing is complete, also shows an item count label at the right edge.
    /// </summary>
    internal void RefreshStatusBarHints()
    {
        RemoveItemCountLabel();

        var statusBar = GetCurrentStatusBar();
        if (statusBar is null)
        {
            return;
        }

        // Clear all existing shortcuts
        statusBar.RemoveAll();

        List<string> hints = ["o:Open", "s:Save", "q:Quit"];

        if (!string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            AddContextualHints(hints);
        }

        hints.Add("?:Help");

        PopulateShortcuts(statusBar, hints);
        AddItemCountLabel(statusBar);
    }

    private void AddContextualHints(List<string> hints)
    {
        AddTreeToggleHint(hints);
        AddMenuHint(hints);

        var currentActionCount = _state.TryGetDrillDown(out var drillDown)
            ? drillDown.ActionStack.Count
            : _state.ActionStack.Count;

        if (currentActionCount > 0)
        {
            hints.Add("c:Clear");
        }

        if (_state.CurrentMode == ViewMode.FocusedTable)
        {
            hints.Add("bs:Back");
        }
    }

    private void AddTreeToggleHint(List<string> hints)
    {
        var format = FormatDetector.DetectInputFile(_state.CurrentFilePath);
        if (format.IsSuccess
            && format.Value is DataFormat.JsonLines or DataFormat.JsonArray
            && _state.CurrentMode != ViewMode.FocusedTable)
        {
            hints.Add("t:Tree/Table");
        }
    }

    private void AddMenuHint(List<string> hints)
    {
        var currentView = GetCurrentView();
        if ((currentView is MorphTableView && _state.CurrentMode != ViewMode.FocusedTable)
            || currentView is MorphTreeView)
        {
            hints.Add("x:Menu");
        }
    }

    private static void PopulateShortcuts(StatusBar statusBar, List<string> hints)
    {
        // Populate shortcuts with Key.Empty to suppress key indicator
        var shortcuts = hints.Select(static hint => new Shortcut { Key = Key.Empty, HelpText = hint }).ToList();
        foreach (var shortcut in shortcuts)
        {
            statusBar.Add(shortcut);
        }
    }

    private void AddItemCountLabel(StatusBar statusBar)
    {
        if (_state.RowIndexer is not { IsIndexingCompleted: true } || IsFilterScanRunning())
        {
            return;
        }

        // A displayed table reports its own (possibly filtered) count; other views fall back to the file total.
        var rowCount = _currentView is MorphTableView { Table: Views.PagedTableSource paged }
            ? paged.TotalRows
            : _state.RowIndexer.TotalRows;

        _itemCountLabel = new Label
        {
            Text = string.Create(CultureInfo.InvariantCulture, $"{rowCount} items"),
            // AnchorEnd places the right edge at the container boundary; subtract 1 to keep a margin
            X = Pos.AnchorEnd() - 1,
            // Place on the same row as the StatusBar (bottom line of the container)
            Y = Pos.AnchorEnd(1),
            SchemeName = statusBar.SchemeName,
        };
        _container.Add(_itemCountLabel);
    }

    private bool IsFilterScanRunning() =>
        _filterScanManager.IsScanRunning
        || (_lazyScanView is not null && ReferenceEquals(_lazyScanView, _currentView));

    /// <summary>
    /// Runs the lazy filter scan for <paramref name="view"/> and, once it ends, hands the outcome to the
    /// UI thread, which refreshes the item count label only for a successful scan of the still-current view.
    /// </summary>
    private async Task RunLazyFilterScanAsync(IFilterRowIndexer filterIndexer, View view)
    {
        var scanTask = _lazyFilterScanRunner.RunAsync(filterIndexer, _state.Cts.Token);

        // WhenAny never throws, so a canceled or faulted scan is observed without a catch-all.
        await Task.WhenAny(scanTask).ConfigureAwait(false);
        var succeeded = scanTask.IsCompletedSuccessfully;

        _uiThreadInvoke(() => CompleteLazyFilterScan(view, succeeded));
    }

    private void CompleteLazyFilterScan(View view, bool succeeded)
    {
        if (!succeeded || _disposed || !ReferenceEquals(_currentView, view) || !ReferenceEquals(_lazyScanView, view))
        {
            return;
        }

        _lazyScanView = null;
        RefreshStatusBarHints();
    }

    /// <summary>
    /// Updates the breadcrumb bar to reflect the current location and stores it on <see cref="AppState"/>.
    /// </summary>
    /// <param name="path">The ordered path segments from root to the current location.</param>
    /// <param name="collapseIndices">
    /// When <c>true</c>, array indices render as <c>"[*]"</c> (Full Aggregation DrillDown).
    /// </param>
    internal void UpdateBreadcrumb(IReadOnlyList<KeyPathSegment> path, bool collapseIndices)
    {
        _state.SetCurrentKeyPath(path);
        _breadcrumbBar.SetPath(path, collapseIndices);
    }

    /// <summary>
    /// Blanks the breadcrumb bar and resets <see cref="AppState.CurrentKeyPath"/> for modes with no
    /// JSON hierarchy (<see cref="ViewMode.CsvTable"/>, <see cref="ViewMode.JsonLinesTable"/>,
    /// <see cref="ViewMode.FileSelection"/>). Unlike <see cref="UpdateBreadcrumb"/> with an empty
    /// path, this does not render <c>"root"</c>.
    /// </summary>
    internal void ClearBreadcrumb()
    {
        _state.SetCurrentKeyPath([]);
        _breadcrumbBar.Clear();
    }

    /// <summary>
    /// Toggles between JSON Lines Tree and Table view modes.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal async Task ToggleJsonLinesModeAsync()
    {
        var result = await _modeController.ToggleJsonLinesModeAsync().ConfigureAwait(false);

        _uiThreadInvoke(() =>
        {
            if (result.IsFailure)
            {
                ShowError(result.Error);
                RefreshStatusBarHints();
                return;
            }

            if (_state.CurrentMode == ViewMode.JsonLinesTree && _state.RowIndexer is not null)
            {
                SwitchToJsonLinesTree(_state.RowIndexer);
                return;
            }

            if (
                _state.CurrentMode == ViewMode.JsonLinesTable
                && _state.RowIndexer is not null
                && _state.Schema is not null)
            {
                SwitchToJsonLinesTableView(_state.RowIndexer, _state.Schema);
            }
        });
    }

    /// <summary>
    /// Toggles between JSON Array Tree and Table view modes.
    /// Table view is not supported; returns a completed task immediately.
    /// </summary>
    /// <returns>A completed task.</returns>
    internal Task ToggleJsonArrayModeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Switches the content area to the initial file-selection prompt.
    /// </summary>
    internal void SwitchToFileSelection()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ClearBreadcrumb();
        SwapView(Views.FileSelectionView.Create());
        RefreshStatusBarHints();
    }

    /// <summary>
    /// Switches the content area to the virtualized CSV table view.
    /// Wraps the source with <see cref="Views.LazyTransformer"/> when the Action Stack is non-empty.
    /// </summary>
    /// <param name="indexer">The CSV row indexer for the loaded file.</param>
    /// <param name="schema">The detected table schema.</param>
    internal void SwitchToCsvTable(IRowIndexer indexer, TableSchema schema)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(schema);
        ClearBreadcrumb();

        IExtendedTableSource rawSource = new Views.VirtualTableSource(indexer, schema);
        var source = _state.ActionStack.Count > 0
            ? Views.LazyTransformer.Create(
                rawSource,
                schema,
                _state.ActionStack,
                filterSpecs => new Refedle.Engine.IO.Csv.FilterRowIndexer(
                    indexer,
                    schema.Columns.Count,
                    filterSpecs
                )
            )
            : rawSource;

        var style = new TableStyle { AlwaysShowHeaders = true };
        var view = new Views.CsvTableView
        {
            Table = new Views.PagedTableSource(new Views.ColumnWidthStabilizingTableSource(source, style)),
            Style = style,
            OnMorphAction = HandleMorphAction,
        };
        SetInitialSelectionWhenReady(view, indexer);
        SwapView(view);
        view.SetFocus();
        StartLazyFilterScan(source, view);
    }

    /// <summary>
    /// Switches the content area to the JSON Lines hierarchical tree view.
    /// </summary>
    /// <param name="indexer">The JSON Lines row indexer for the loaded file.</param>
    internal void SwitchToJsonLinesTree(IRowIndexer indexer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(indexer);

        var view = Views.JsonLinesTreeView.Create(
            indexer,
            () => _ = ToggleJsonLinesModeAsync(),
            path => UpdateBreadcrumb(path, collapseIndices: false),
            _uiThreadInvoke);
        UpdateBreadcrumb([], collapseIndices: false);
        SwapView(view);
        RefreshStatusBarHints();
    }

    /// <summary>
    /// Switches the content area to the JSON Array hierarchical tree view.
    /// </summary>
    /// <param name="indexer">The JSON Array row indexer for the loaded file.</param>
    internal void SwitchToJsonArrayTree(IRowIndexer indexer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(indexer);

        var view = Views.JsonArrayTreeView.Create(
            indexer,
            () => _ = ToggleJsonArrayModeAsync(),
            path => UpdateBreadcrumb(path, collapseIndices: false),
            _uiThreadInvoke);
        UpdateBreadcrumb([], collapseIndices: false);
        SwapView(view);
        RefreshStatusBarHints();
    }

    /// <summary>
    /// Switches the content area to the JSON Object hierarchical tree view.
    /// Table mode is not supported for JSON Object; a no-op callback is passed inline.
    /// </summary>
    /// <param name="entries">
    /// The key-value pairs returned by <see cref="Engine.IO.JsonObject.TopLevelScanner.Scan"/>.
    /// </param>
    internal void SwitchToJsonObjectTree(
        IReadOnlyList<JsonObjectEntry> entries)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(entries);
        var view = Views.JsonObjectTreeView.Create(
            entries,
            static () => { },
            path => UpdateBreadcrumb(path, collapseIndices: false));
        UpdateBreadcrumb([], collapseIndices: false);
        SwapView(view);
        RefreshStatusBarHints();
    }

    /// <summary>
    /// Switches the content area to the JSON Lines table view.
    /// Wraps the source with <see cref="Views.LazyTransformer"/> when the Action Stack is non-empty.
    /// Registers <see cref="AppState.OnSchemaRefined"/> for background schema updates.
    /// </summary>
    /// <param name="indexer">The JSON Lines row indexer for the loaded file.</param>
    /// <param name="schema">The detected table schema.</param>
    internal void SwitchToJsonLinesTableView(IRowIndexer indexer, TableSchema schema)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(schema);
        ClearBreadcrumb();

        var cache = new RowByteCache(indexer);
        var source = new Views.JsonLinesTableSource(cache, schema);

        IExtendedTableSource tableSource = _state.ActionStack.Count > 0
            ? Views.LazyTransformer.Create(
                source,
                schema,
                _state.ActionStack,
                filterSpecs => new Refedle.Engine.IO.JsonLines.FilterRowIndexer(
                    indexer,
                    indexer.FilePath,
                    [.. schema.Columns.Select(static c => Encoding.UTF8.GetBytes(c.Name))],
                    filterSpecs
                )
            )
            : source;

        var style = new TableStyle { AlwaysShowHeaders = true };
        var view = new Views.JsonLinesTableView
        {
            Table = new Views.PagedTableSource(new Views.ColumnWidthStabilizingTableSource(tableSource, style)),
            Style = style,
            OnMorphAction = HandleMorphAction,
        };
        _state.SetSchemaRefinedCallback(refined =>
        {
            source.UpdateSchema(refined);
            if (tableSource is Views.LazyTransformer lazy)
            {
                RebuildLazyTransformer(view, source, lazy, refined);
            }
        });
        SetInitialSelectionWhenReady(view, indexer);
        SwapView(view);
        view.SetFocus();
        StartLazyFilterScan(tableSource, view);
    }

    private void StartLazyFilterScan(IExtendedTableSource source, View view)
    {
        // Marked before the refresh so the label stays hidden until the scan completes.
        _lazyScanView = source is Views.LazyTransformer { FilterRowIndexer: not null } ? view : null;
        RefreshStatusBarHints();

        if (source is Views.LazyTransformer { FilterRowIndexer: { } filterIndexer })
        {
            _ = RunLazyFilterScanAsync(filterIndexer, view);
        }
    }

    /// <summary>
    /// Replaces only the table source of <paramref name="view"/> with a <see cref="Views.LazyTransformer"/>
    /// built from the refined schema, so columns discovered after indexing become visible.
    /// The existing filter row indexer is reused, and the page and selection are carried over.
    /// </summary>
    private void RebuildLazyTransformer(
        Views.JsonLinesTableView view,
        Views.JsonLinesTableSource source,
        Views.LazyTransformer current,
        TableSchema refined)
    {
        if (_currentView != view || view.Table is not Views.PagedTableSource oldPaged)
        {
            return;
        }

        var pageNumber = oldPaged.CurrentPageNumber;
        var selection = view.Value?.SelectedCell;
        var rowOffset = view.RowOffset;
        var columnOffset = view.ColumnOffset;

        Func<IReadOnlyList<FilterSpec>, IFilterRowIndexer>? reuseFilterIndexer =
            current.FilterRowIndexer is { } filterIndexer ? _ => filterIndexer : null;
        IExtendedTableSource rebuilt = Views.LazyTransformer.Create(source, refined, _state.ActionStack, reuseFilterIndexer);
        view.Table = new Views.PagedTableSource(new Views.ColumnWidthStabilizingTableSource(rebuilt, view.Style));
        var newPaged = (Views.PagedTableSource)view.Table;
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

    /// <summary>
    /// Refreshes the current table view by re-invoking the appropriate <c>SwitchTo*</c> method.
    /// Called after a morph action is added to <see cref="AppState.ActionStack"/> or the active
    /// <see cref="DrillDownState.ActionStack"/> so that <see cref="Views.LazyTransformer"/> or
    /// <see cref="Views.FocusedTableTransformer"/> is reconstructed with the updated stack.
    /// </summary>
    internal void RefreshCurrentTableView()
    {
        switch (_state.CurrentMode)
        {
            case ViewMode.CsvTable when _state.RowIndexer is not null && _state.Schema is not null:
                SwitchToCsvTable(_state.RowIndexer, _state.Schema);
                break;

            case ViewMode.JsonLinesTable
                when _state.RowIndexer is not null && _state.Schema is not null:
                SwitchToJsonLinesTableView(_state.RowIndexer, _state.Schema);
                break;

            case ViewMode.FocusedTable when _state.TryGetDrillDown(out var drillDown):
                SwitchToFocusedTable(drillDown);
                break;

            default:
                // Reached from mode-independent callers (e.g. global "clear actions" shortcut,
                // recipe load) when the current view isn't a table (tree view, file selection, etc.);
                // no-op preserves the existing view instead of crashing.
                break;
        }
    }

    /// <summary>
    /// Handles a column morphing action from a table view.
    /// Appends the action to the stack and refreshes the current view.
    /// </summary>
    /// <param name="action">The morph action to apply.</param>
    private void HandleMorphAction(IMorphAction action)
    {
        if (_state.TryGetDrillDown(out _))
        {
            _state.AddDrillDownAction(action);
            RefreshCurrentTableView();
            return;
        }

        _state.AddMorphAction(action);
        RefreshCurrentTableView();
    }

    /// <summary>
    /// Displays an error message in a placeholder view.
    /// </summary>
    /// <param name="message">The error message to display.</param>
    internal void ShowError(string message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(message);

        _state.EnterPlaceholderMode();
        var view = Views.PlaceholderView.Create(_state);
        view.Text = message;
        SwapView(view);
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

            _uiThreadInvoke(() =>
            {
                // Only set if this view is still active and the user hasn't moved the cursor yet
                if (_currentView == view && view.Table is not null && view.Table.Rows > 0
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

    private void SwapView(View newView)
    {
        // Replacing the content view orphans a running filter scan, except when the new view
        // is the scan's own staged replacement for the loading view.
        _filterScanManager.CancelIfNotStaged(newView);

        if (_currentView is not null)
        {
            _contentContainer.Remove(_currentView);
            _currentView.Dispose();
        }

        newView.X = 0;
        newView.Y = 0;
        newView.Width = Dim.Fill();
        newView.Height = Dim.Fill();

        _currentView = newView;
        _contentContainer.Add(_currentView);
        _contentContainer.SetNeedsDraw();
    }

    private void RemoveItemCountLabel()
    {
        if (_itemCountLabel is null)
        {
            return;
        }

        _container.Remove(_itemCountLabel);
        _itemCountLabel.Dispose();
        _itemCountLabel = null;
    }

    /// <summary>
    /// Gets the current view.
    /// </summary>
    /// <returns>The current <see cref="View"/>, or <c>null</c>.</returns>
    internal View? GetCurrentView() => _currentView;

    /// <summary>
    /// Gets the current status bar.
    /// </summary>
    /// <returns>The current <see cref="StatusBar"/>, or <c>null</c>.</returns>
    internal StatusBar? GetCurrentStatusBar()
    {
        return _container.SubViews.OfType<StatusBar>().FirstOrDefault();
    }

    /// <summary>
    /// Orchestrates the Single DrillDown transition: delegates schema extraction to ModeController,
    /// then switches to FocusedTable view on the UI thread.
    /// </summary>
    internal void DrillDown(SingleDrillDownRequest request)
    {
        var result = _modeController.DrillDown(request);

        _uiThreadInvoke(() =>
        {
            if (result.IsFailure)
            {
                ShowError(result.Error);
                return;
            }

            if (!_state.TryGetDrillDown(out var drillDown))
            {
                throw new UnreachableException(
                    "ModeController.DrillDown must set DrillDown state on success.");
            }

            UpdateBreadcrumb(request.KeyPath, collapseIndices: false);
            SwitchToFocusedTable(drillDown);
        });
    }

    /// <summary>
    /// Orchestrates the Full Aggregation DrillDown transition: offloads file scan to a background thread
    /// via ModeController, then applies state and switches to FocusedTable view on the UI thread.
    /// </summary>
    internal async ValueTask FullAggregationDrillDownAsync(FullAggregationDrillDownRequest request)
    {
        var result = await _modeController.FullAggregationDrillDownAsync(request).ConfigureAwait(false);
        _uiThreadInvoke(() =>
        {
            // Fail fast before any state mutation: if the ViewManager was disposed between the
            // background scan completing and this callback running, leave AppState untouched.
            // SwitchToFocusedTable has its own guard, but state is written here first, so without
            // this check a dispose race would corrupt AppState before the view-side throw.
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (result.IsFailure)
            {
                ShowError(result.Error);
                return;
            }

            _state.EnterFocusedTable(result.Value);
            UpdateBreadcrumb(request.KeyPath, collapseIndices: true);
            SwitchToFocusedTable(result.Value);
        });
    }

    /// <summary>
    /// Creates FocusedTableSource and FocusedTableView, then switches to the FocusedTable view.
    /// Wraps the source with <see cref="Views.FocusedTableTransformer"/> when the Action Stack is non-empty.
    /// With an active filter, a loading view plus a progress overlay is shown while the background
    /// scan runs; the table replaces them once enough rows have matched or the scan finishes.
    /// Must be called on the UI thread (see the class threading contract).
    /// </summary>
    internal void SwitchToFocusedTable(DrillDownState drillDown)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IExtendedTableSource rawSource = new Views.FocusedTableSource(drillDown);
        var source = drillDown.ActionStack.Count > 0
            ? Views.FocusedTableTransformer.Create(
                rawSource,
                drillDown.Schema,
                drillDown.ActionStack,
                FilterScanInitialTableRows)
            : rawSource;

        var style = new TableStyle { AlwaysShowHeaders = true };
        _state.SetSchemaRefinedCallback(null);

        if (source is Views.FocusedTableTransformer
            {
                FilterRowIndexer: Views.FocusedTableFilterRowIndexer indexer,
            } transformer)
        {
            _filterScanManager.StartScan(transformer, indexer, style);
            return;
        }

        var view = CreateFocusedTableView(source, style);
        view.SetSelection(0, 0, false);
        view.Update();
        SwapView(view);
        view.SetFocus();
        RefreshStatusBarHints();
    }

    private Views.FocusedTableView CreateFocusedTableView(IExtendedTableSource source, TableStyle style)
    {
        var view = new Views.FocusedTableView
        {
            Table = new Views.PagedTableSource(new Views.ColumnWidthStabilizingTableSource(source, style)),
            Style = style,
            OnMorphAction = HandleMorphAction,
        };
        return view;
    }

    /// <summary>
    /// Returns from <see cref="ViewMode.FocusedTable"/> to the tree mode the active DrillDown was
    /// entered from, rebuilding that tree from its cached backing data on <see cref="AppState"/>.
    /// A no-op when there is no active DrillDown session.
    /// </summary>
    internal void ReturnFromDrillDown()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_state.TryGetDrillDown(out var drillDown))
        {
            return;
        }

        _state.ExitFocusedTable(drillDown.PreviousMode);

        switch (drillDown.PreviousMode)
        {
            case ViewMode.JsonLinesTree when _state.RowIndexer is not null:
                SwitchToJsonLinesTree(_state.RowIndexer);
                break;

            case ViewMode.JsonArrayTree when _state.RowIndexer is not null:
                SwitchToJsonArrayTree(_state.RowIndexer);
                break;

            case ViewMode.JsonObjectTree when _state.JsonObjectEntries is not null:
                SwitchToJsonObjectTree(_state.JsonObjectEntries);
                break;

            default:
                throw new UnreachableException(
                    "DrillDownState.PreviousMode must be a tree mode with its backing data still cached on AppState.");
        }
    }

    /// <inheritdoc/>
    /// <remarks>Must be called on the UI thread (see the class threading contract).</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _filterScanManager.Dispose();
        RemoveItemCountLabel();
        _currentView?.Dispose();
    }
}

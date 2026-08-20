using System.Diagnostics;
using System.Globalization;
using System.Text;
using Refedle.App.Views;
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

namespace Refedle.App;

/// <summary>
/// Manages the active content view inside a Terminal.Gui <see cref="Window"/>.
/// Reads Engine-layer objects from <see cref="AppState"/> and switches the visible view accordingly.
/// Has no dependency on the Engine layer directly.
/// </summary>
internal sealed class ViewManager : IDisposable
{
    private readonly Window _container;
    private readonly AppState _state;
    private readonly ModeController _modeController;
    private readonly Action<Action> _uiThreadInvoke;
    private readonly BreadcrumbBar _breadcrumbBar;
    private readonly View _contentContainer;
    private View? _currentView;
    private Label? _itemCountLabel;
    private bool _disposed;

    internal ViewManager(Window container, AppState state, ModeController modeController, Action<Action> uiThreadInvoke)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(modeController);
        ArgumentNullException.ThrowIfNull(uiThreadInvoke);
        _container = container;
        _state = state;
        _modeController = modeController;
        _uiThreadInvoke = uiThreadInvoke;
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
        var format = FormatDetector.Detect(_state.CurrentFilePath);
        if (format.IsSuccess
            && format.Value is DataFormat.JsonLines or DataFormat.JsonArray
            && _state.CurrentMode != ViewMode.FocusedTable)
        {
            hints.Add("t:Tree/Table");
        }

        var currentView = GetCurrentView();
        if ((currentView is MorphTableView && _state.CurrentMode != ViewMode.FocusedTable)
            || currentView is MorphTreeView)
        {
            hints.Add("x:Menu");
        }

        if (_state.ActionStack.Count > 0)
        {
            hints.Add("c:Clear");
        }

        if (_state.CurrentMode == ViewMode.FocusedTable)
        {
            hints.Add("bs:Back");
        }
    }

    private static void PopulateShortcuts(StatusBar statusBar, List<string> hints)
    {
        // Populate shortcuts with Key.Empty to suppress key indicator
        var shortcuts = hints.Select(hint => new Shortcut { Key = Key.Empty, HelpText = hint }).ToList();
        foreach (var shortcut in shortcuts)
        {
            statusBar.Add(shortcut);
        }
    }

    private void AddItemCountLabel(StatusBar statusBar)
    {
        if (_state.RowIndexer is not { IsIndexingCompleted: true })
        {
            return;
        }

        _itemCountLabel = new Label
        {
            Text = string.Create(CultureInfo.InvariantCulture, $"{_state.RowIndexer.TotalRows} items"),
            // AnchorEnd places the right edge at the container boundary; subtract 1 to keep a margin
            X = Pos.AnchorEnd() - 1,
            // Place on the same row as the StatusBar (bottom line of the container)
            Y = Pos.AnchorEnd(1),
            SchemeName = statusBar.SchemeName,
        };
        _container.Add(_itemCountLabel);
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
        _state.CurrentKeyPath = path;
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
        _state.CurrentKeyPath = [];
        _breadcrumbBar.Clear();
    }

    /// <summary>
    /// Toggles between JSON Lines Tree and Table view modes.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal async Task ToggleJsonLinesModeAsync()
    {
        var result = await _modeController.ToggleJsonLinesModeAsync();

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

        ITableSource rawSource = new Views.VirtualTableSource(indexer, schema);
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

        Func<int, string> getRawColumnName = source switch
        {
            Views.LazyTransformer lt => i => lt.RawColumnNames[i],
            Views.VirtualTableSource vts => i => vts.RawColumnNames[i],
            _ => throw new UnreachableException(),
        };

        var view = new Views.CsvTableView
        {
            Table = source,
            Style = new TableStyle { AlwaysShowHeaders = true },
            OnMorphAction = HandleMorphAction,
            GetRawColumnName = getRawColumnName,
        };
        SetInitialSelectionWhenReady(view, indexer);
        SwapView(view);
        view.SetFocus();
        RefreshStatusBarHints();

        if (source is Views.LazyTransformer { FilterRowIndexer: { } filterIndexer })
        {
            _ = Task.Run(() => filterIndexer.BuildIndexAsync(_state.Cts.Token), _state.Cts.Token);
        }
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
        _state.OnSchemaRefined = source.UpdateSchema;

        var tableSource = _state.ActionStack.Count > 0
            ? (ITableSource)Views.LazyTransformer.Create(
                source,
                schema,
                _state.ActionStack,
                filterSpecs => new Refedle.Engine.IO.JsonLines.FilterRowIndexer(
                    indexer,
                    indexer.FilePath,
                    [.. schema.Columns.Select(c => Encoding.UTF8.GetBytes(c.Name))],
                    filterSpecs
                )
            )
            : source;

        Func<int, string> getRawColumnName = tableSource switch
        {
            Views.LazyTransformer lt => i => lt.RawColumnNames[i],
            Views.JsonLinesTableSource jts => i => jts.RawColumnNames[i],
            _ => throw new UnreachableException(),
        };

        var view = new Views.JsonLinesTableView
        {
            Table = tableSource,
            Style = new TableStyle { AlwaysShowHeaders = true },
            OnMorphAction = HandleMorphAction,
            GetRawColumnName = getRawColumnName,
        };
        SetInitialSelectionWhenReady(view, indexer);
        SwapView(view);
        view.SetFocus();
        RefreshStatusBarHints();

        if (tableSource is Views.LazyTransformer { FilterRowIndexer: { } filterIndexer })
        {
            _ = Task.Run(() => filterIndexer.BuildIndexAsync(_state.Cts.Token), _state.Cts.Token);
        }
    }

    /// <summary>
    /// Refreshes the current table view by re-invoking the appropriate <c>SwitchTo*</c> method.
    /// Called after a morph action is added to <see cref="AppState.ActionStack"/> so that
    /// <see cref="Views.LazyTransformer"/> or <see cref="Views.FocusedTableTransformer"/>
    /// is reconstructed with the updated stack.
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

            case ViewMode.FocusedTable when _state.DrillDown is not null:
                SwitchToFocusedTable(_state.DrillDown);
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
    private void HandleMorphAction(MorphAction action)
    {
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

        _state.CurrentMode = ViewMode.PlaceholderView;
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

            if (_state.DrillDown is not { } drillDown)
            {
                throw new UnreachableException(
                    "ModeController.DrillDown must set DrillDown state on success.");
            }

            _state.ClearMorphActions();
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
        var result = await _modeController.FullAggregationDrillDownAsync(request);
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

            _state.DrillDown = result.Value;
            _state.CurrentMode = ViewMode.FocusedTable;
            _state.ClearMorphActions();
            UpdateBreadcrumb(request.KeyPath, collapseIndices: true);
            SwitchToFocusedTable(result.Value);
        });
    }

    /// <summary>
    /// Creates FocusedTableSource and FocusedTableView, then switches to the FocusedTable view.
    /// Wraps the source with <see cref="Views.FocusedTableTransformer"/> when the Action Stack is non-empty.
    /// </summary>
    internal void SwitchToFocusedTable(DrillDownState drillDown)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ITableSource rawSource = new Views.FocusedTableSource(drillDown);
        var source = _state.ActionStack.Count > 0
            ? Views.FocusedTableTransformer.Create(rawSource, drillDown.Schema, _state.ActionStack)
            : rawSource;

        Func<int, string> getRawColumnName = source switch
        {
            Views.FocusedTableTransformer ft => i => ft.RawColumnNames[i],
            Views.FocusedTableSource fts => i => fts.RawColumnNames[i],
            _ => throw new UnreachableException(),
        };

        var view = new Views.FocusedTableView
        {
            Table = source,
            Style = new TableStyle { AlwaysShowHeaders = true },
            OnMorphAction = HandleMorphAction,
            GetRawColumnName = getRawColumnName,
        };
        _state.OnSchemaRefined = null;
        view.SetSelection(0, 0, false);
        view.Update();
        SwapView(view);
        view.SetFocus();
        RefreshStatusBarHints();
    }

    /// <summary>
    /// Returns from <see cref="ViewMode.FocusedTable"/> to the tree mode the active DrillDown was
    /// entered from, rebuilding that tree from its cached backing data on <see cref="AppState"/>.
    /// A no-op when there is no active DrillDown session.
    /// </summary>
    internal void ReturnFromDrillDown()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_state.DrillDown is not { } drillDown)
        {
            return;
        }

        _state.DrillDown = null;

        switch (drillDown.PreviousMode)
        {
            case ViewMode.JsonLinesTree when _state.RowIndexer is not null:
                _state.CurrentMode = ViewMode.JsonLinesTree;
                SwitchToJsonLinesTree(_state.RowIndexer);
                break;

            case ViewMode.JsonArrayTree when _state.RowIndexer is not null:
                _state.CurrentMode = ViewMode.JsonArrayTree;
                SwitchToJsonArrayTree(_state.RowIndexer);
                break;

            case ViewMode.JsonObjectTree when _state.JsonObjectEntries is not null:
                _state.CurrentMode = ViewMode.JsonObjectTree;
                SwitchToJsonObjectTree(_state.JsonObjectEntries);
                break;

            default:
                throw new UnreachableException(
                    "DrillDownState.PreviousMode must be a tree mode with its backing data still cached on AppState.");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RemoveItemCountLabel();
        _currentView?.Dispose();
    }
}

using System.Globalization;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine;
using Refedle.Engine.Types;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Key = Terminal.Gui.Input.Key;

namespace Refedle.App.Tui.UI;

/// <summary>
/// Populates the status bar hints and the item count label of the window.
/// </summary>
/// <param name="container">The window that hosts the status bar and the item count label.</param>
/// <param name="state">The application state the hints are derived from.</param>
/// <param name="getCurrentView">Returns the content view currently shown.</param>
/// <param name="isFilterScanRunning">Returns whether a filter scan is running for the current view.</param>
/// <remarks>
/// Threading contract: all state, including the item count label, is UI-thread exclusive.
/// <see cref="Refresh"/>, <see cref="GetCurrentStatusBar"/> and <see cref="RemoveItemCountLabel"/>
/// must be called on the UI thread.
/// </remarks>
internal sealed class StatusBarPresenter(
    Window container,
    AppState state,
    Func<View?> getCurrentView,
    Func<bool> isFilterScanRunning)
{
    private Label? _itemCountLabel;

    /// <summary>
    /// Refreshes the status bar hints based on the current application state.
    /// When indexing is complete, also shows an item count label at the right edge.
    /// </summary>
    internal void Refresh()
    {
        RemoveItemCountLabel();

        StatusBar? statusBar = GetCurrentStatusBar();
        if (statusBar is null)
        {
            return;
        }

        // Clear all existing shortcuts
        statusBar.RemoveAll();

        List<string> hints = ["o:Open", "s:Save", "q:Quit"];

        if (!string.IsNullOrWhiteSpace(state.CurrentFilePath))
        {
            AddContextualHints(hints);
        }

        hints.Add("?:Help");

        PopulateShortcuts(statusBar, hints);
        AddItemCountLabel(statusBar);
    }

    /// <summary>
    /// Gets the current status bar.
    /// </summary>
    /// <returns>The current <see cref="StatusBar"/>, or <c>null</c>.</returns>
    internal StatusBar? GetCurrentStatusBar()
    {
        return container.SubViews.OfType<StatusBar>().FirstOrDefault();
    }

    /// <summary>
    /// Removes the item count label from the window, if shown.
    /// </summary>
    internal void RemoveItemCountLabel()
    {
        if (_itemCountLabel is null)
        {
            return;
        }

        container.Remove(_itemCountLabel);
        _itemCountLabel.Dispose();
        _itemCountLabel = null;
    }

    private void AddContextualHints(List<string> hints)
    {
        AddTreeToggleHint(hints);
        AddMenuHint(hints);

        int currentActionCount = state.TryGetDrillDown(out DrillDownState? drillDown)
            ? drillDown.ActionStack.Count
            : state.ActionStack.Count;

        if (currentActionCount > 0)
        {
            hints.Add("c:Clear");
        }

        if (state.CurrentMode == ViewMode.FocusedTable)
        {
            hints.Add("bs:Back");
        }
    }

    private void AddTreeToggleHint(List<string> hints)
    {
        Result<DataFormat> format = FormatDetector.DetectInputFile(state.CurrentFilePath);
        if (format.IsSuccess
            && format.Value is DataFormat.JsonLines or DataFormat.JsonArray
            && state.CurrentMode != ViewMode.FocusedTable)
        {
            hints.Add("t:Tree/Table");
        }
    }

    private void AddMenuHint(List<string> hints)
    {
        View? currentView = getCurrentView();
        if ((currentView is MorphTableView && state.CurrentMode != ViewMode.FocusedTable)
            || currentView is MorphTreeView)
        {
            hints.Add("x:Menu");
        }
    }

    private static void PopulateShortcuts(StatusBar statusBar, List<string> hints)
    {
        // Populate shortcuts with Key.Empty to suppress key indicator
        List<Shortcut> shortcuts = [.. hints.Select(static hint => new Shortcut { Key = Key.Empty, HelpText = hint })];
        foreach (Shortcut shortcut in shortcuts)
        {
            statusBar.Add(shortcut);
        }
    }

    private void AddItemCountLabel(StatusBar statusBar)
    {
        if (state.RowIndexer is not { IsIndexingCompleted: true } || isFilterScanRunning())
        {
            return;
        }

        // A displayed table reports its own (possibly filtered) count; other views fall back to the file total.
        long rowCount = getCurrentView() is MorphTableView { Table: PagedTableSource paged }
            ? paged.TotalRows
            : state.RowIndexer.TotalRows;

        _itemCountLabel = new Label
        {
            Text = string.Create(CultureInfo.InvariantCulture, $"{rowCount} items"),
            // AnchorEnd places the right edge at the container boundary; subtract 1 to keep a margin
            X = Pos.AnchorEnd() - 1,
            // Place on the same row as the StatusBar (bottom line of the container)
            Y = Pos.AnchorEnd(1),
            SchemeName = statusBar.SchemeName,
        };
        container.Add(_itemCountLabel);
    }
}

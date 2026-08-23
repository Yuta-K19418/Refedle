using Refedle.App.Views;
using Refedle.App.Views.Dialogs;
using Refedle.App.Views.JsonTreeNodes;
using Refedle.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.App;

/// <summary>
/// Handles global keyboard shortcuts for Refedle application.
/// </summary>
internal sealed class AppKeyHandler : IDisposable
{
    private readonly IApplication _app;
    private readonly AppState _state;
    private readonly ViewManager _viewManager;
    private readonly FileDialogHandler _fileDialogHandler;
    private readonly RecipeCommandHandler _recipeCommandHandler;

    private bool _disposed;

    /// <summary>
    /// Determines whether the specified key code corresponds to a global application shortcut.
    /// </summary>
    /// <param name="keyCode">The key code to check.</param>
    /// <returns><c>true</c> if the key is a global shortcut; <c>false</c> otherwise.</returns>
    internal static bool IsGlobalShortcut(KeyCode keyCode)
    {
        var baseKey = keyCode & ~(KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask);
        return baseKey is KeyCode.O or KeyCode.S or KeyCode.Q or KeyCode.T or KeyCode.X or KeyCode.C or KeyCode.Backspace or (KeyCode)'?';
    }

    internal AppKeyHandler(
        IApplication app,
        AppState state,
        ViewManager viewManager,
        FileDialogHandler fileDialogHandler,
        RecipeCommandHandler recipeCommandHandler
    )
    {
        _app = app;
        _state = state;
        _viewManager = viewManager;
        _fileDialogHandler = fileDialogHandler;
        _recipeCommandHandler = recipeCommandHandler;
    }

    internal void Subscribe()
    {
        _app.Keyboard.KeyDown -= OnGlobalKeyDown;
        _app.Keyboard.KeyDown += OnGlobalKeyDown;
    }

    /// <summary>
    /// Handles quit shortcut (q).
    /// Confirms with user if there are unsaved changes.
    /// </summary>
    /// <returns><c>true</c> if the key was handled; <c>false</c> otherwise.</returns>
    private bool HandleQuit()
    {
        var currentActionCount = _state.CurrentMode == ViewMode.FocusedTable && _state.DrillDown is { } drillDown
            ? drillDown.ActionStack.Count
            : _state.ActionStack.Count;

        if (currentActionCount == 0)
        {
            _app.RequestStop();
            return true;
        }

        var result = MessageBox.Query(
            _app,
            "Quit",
            "You have unsaved changes in your recipe. Quit anyway?",
            "Yes",
            "No"
        );
        if (result == 0)
        {
            _app.RequestStop();
        }

        return true;
    }

    /// <summary>
    /// Handles help overlay shortcut (?).
    /// </summary>
    /// <returns><c>true</c> if the key was handled; <c>false</c> otherwise.</returns>
    private bool HandleHelp()
    {
        var dialog = new HelpDialog();
        _app.Run(dialog);
        return true;
    }

    private bool HandleOpen()
    {
        _ = _fileDialogHandler.ShowAsync().ContinueWith(
            t =>
            {
                if (t.IsFaulted && t.Exception is not null)
                {
                    _app.Invoke(() => _viewManager.ShowError(t.Exception.InnerException?.Message ?? t.Exception.Message));
                }
            },
            TaskScheduler.Default
        );
        return true;
    }

    private bool HandleSave()
    {
        _ = _recipeCommandHandler.SaveAsync().ContinueWith(
            t =>
            {
                if (t.IsFaulted && t.Exception is not null)
                {
                    _app.Invoke(() => _viewManager.ShowError(t.Exception.InnerException?.Message ?? t.Exception.Message));
                }
            },
            TaskScheduler.Default
        );
        return true;
    }

    private bool HandleViewToggle()
    {
        if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            return false;
        }

        var format = FormatDetector.Detect(_state.CurrentFilePath);
        if (format.IsSuccess && format.Value == DataFormat.JsonLines)
        {
            _ = _viewManager.ToggleJsonLinesModeAsync().ContinueWith(
                t =>
                {
                    if (t.IsFaulted && t.Exception is not null)
                    {
                        _app.Invoke(() => _viewManager.ShowError(t.Exception.InnerException?.Message ?? t.Exception.Message));
                    }
                },
                TaskScheduler.Default
            );
            return true;
        }

        return false;
    }

    internal bool HandleActionMenu()
    {
        var currentView = _viewManager.GetCurrentView();

        if (currentView is MorphTableView mt)
        {
            return HandleActionMenuForTable(mt);
        }

        if (currentView is MorphTreeView tv)
        {
            return HandleActionMenuForTree(tv);
        }

        return false;
    }

    private bool HandleActionMenuForTable(MorphTableView mt)
    {
        if (mt.Table is null || mt.GetRawColumnName is null
            || mt.OnMorphAction is null || mt.Value is null)
        {
            return false;
        }

        var format = FormatDetector.Detect(_state.CurrentFilePath);
        if (format.IsFailure)
        {
            _app.Invoke(() => _viewManager.ShowError(format.Error));
            return false;
        }

        var handler = new ColumnActionHandler(
            _app, mt.Table, mt.Value.SelectedCell.X,
            mt.GetRawColumnName, mt.OnMorphAction, format.Value, mt.IsRowIndexComplete);

        var dialog = new ActionMenuDialog(ColumnActionHandler.GetAvailableActions(), handler.ExecuteAction);
        _app.Run(dialog);
        return true;
    }

    private bool HandleActionMenuForTree(MorphTreeView tv)
    {
        if (tv.SelectedObject is not ITreeNode selectedNode)
        {
            return false;
        }

        var treeFormat = FormatDetector.Detect(_state.CurrentFilePath);
        if (treeFormat.IsFailure)
        {
            _app.Invoke(() => _viewManager.ShowError(treeFormat.Error));
            return false;
        }

        if (treeFormat.Value == DataFormat.JsonObject)
        {
            return HandleSingleDrillDown(selectedNode, treeFormat.Value);
        }

        return HandleFullAggregationDrillDown(selectedNode, treeFormat.Value);
    }

    /// <summary>
    /// Single-node DrillDown: JSON Object format only. Requires the selected node to be a
    /// <see cref="JsonArrayTreeNode"/> whose direct children are all <see cref="JsonObjectTreeNode"/>.
    /// </summary>
    private bool HandleSingleDrillDown(ITreeNode selectedNode, DataFormat format)
    {
        if (selectedNode is not JsonArrayTreeNode arrayNode)
        {
            return false;
        }

        var children = arrayNode.Children;
        if (children.Count == 0)
        {
            return false;
        }

        if (children.Any(c => c is not JsonObjectTreeNode))
        {
            return false;
        }

        var request = new SingleDrillDownRequest(
            Format: format,
            NodeBytes: arrayNode.RawJson,
            KeyPath: KeyPathBuilder.Build(selectedNode),
            InitialActionStack: []);

        void onDrillDownConfirmed(string actionName) => _viewManager.DrillDown(request);

        var dialog = new ActionMenuDialog(["DrillDown"], onDrillDownConfirmed);
        _app.Run(dialog);
        return true;
    }

    /// <summary>
    /// Full Aggregation DrillDown: JSON Lines / JSON Array format, any node type, always a full file scan.
    /// </summary>
    private bool HandleFullAggregationDrillDown(ITreeNode selectedNode, DataFormat format)
    {
        var keyPath = KeyPathBuilder.Build(selectedNode);
        var request = new FullAggregationDrillDownRequest(
            Format: format,
            KeyPath: keyPath,
            InitialActionStack: []);

        void onDrillDownConfirmed(string actionName) =>
            _ = _viewManager.FullAggregationDrillDownAsync(request)
                .AsTask()
                .ContinueWith(HandleTaskError, TaskScheduler.Default);

        var dialog = new ActionMenuDialog(["DrillDown"], onDrillDownConfirmed);
        _app.Run(dialog);
        return true;
    }

    /// <summary>
    /// Reports an unhandled exception from a fire-and-forget async operation via the error view.
    /// </summary>
    private void HandleTaskError(Task task)
    {
        if (task.IsFaulted && task.Exception is not null)
        {
            _app.Invoke(() => _viewManager.ShowError(task.Exception.InnerException?.Message ?? task.Exception.Message));
        }
    }

    /// <summary>
    /// Handles the clear action stack shortcut (c).
    /// Shows a confirmation dialog and clears the action stack if confirmed.
    /// Does nothing when the action stack is empty.
    /// </summary>
    /// <returns><c>true</c> if the key was handled; <c>false</c> otherwise.</returns>
    internal bool HandleClearActions()
    {
        var currentActionCount = _state.CurrentMode == ViewMode.FocusedTable && _state.DrillDown is { } drillDown
            ? drillDown.ActionStack.Count
            : _state.ActionStack.Count;

        if (currentActionCount == 0)
        {
            return false;
        }

        var result = MessageBox.Query(
            _app,
            "Clear Actions",
            "Clear all actions from the stack?",
            "Yes",
            "No"
        );
        if (result != 0)
        {
            return true;
        }

        if (_state.CurrentMode == ViewMode.FocusedTable && _state.DrillDown is { } activeDrillDown)
        {
            _state.DrillDown = activeDrillDown with { ActionStack = [] };
            _viewManager.RefreshCurrentTableView();
            return true;
        }

        _state.ClearMorphActions();
        _viewManager.RefreshCurrentTableView();
        return true;
    }

    /// <summary>
    /// Handles back navigation from DrillDown (FocusedTable) via the Backspace key.
    /// </summary>
    /// <returns><c>true</c> if the key was handled; <c>false</c> otherwise.</returns>
    private bool HandleDrillDownBack()
    {
        if (_state.CurrentMode != ViewMode.FocusedTable || _state.DrillDown is null)
        {
            return false;
        }

        _viewManager.ReturnFromDrillDown();
        return true;
    }

    private void OnGlobalKeyDown(object? sender, Key key)
    {
        if (key.Handled)
        {
            return;
        }

        if (IsTextFieldFocused())
        {
            return;
        }

        // Shortcuts like o, s, q, t, x, Backspace should not have Ctrl or Alt modifiers.
        if ((key.KeyCode & (KeyCode.CtrlMask | KeyCode.AltMask)) != KeyCode.Null)
        {
            return;
        }

        key.Handled = DispatchShortcut(key.KeyCode & ~KeyCode.ShiftMask);
    }

    private bool DispatchShortcut(KeyCode baseKey) => baseKey switch
    {
        KeyCode.O => HandleOpen(),
        KeyCode.S => HandleSave(),
        KeyCode.Q => HandleQuit(),
        KeyCode.T => HandleViewToggle(),
        KeyCode.X => HandleActionMenu(),
        KeyCode.C => HandleClearActions(),
        KeyCode.Backspace => HandleDrillDownBack(),
        (KeyCode)'?' => HandleHelp(),
        _ => false,
    };

    // Walks up the SuperView chain since focus may be on a child of the TextField
    // (e.g. its internal cursor/selection handling), not the TextField itself.
    private bool IsTextFieldFocused()
    {
        var focused = _app.Navigation?.GetFocused() ?? _app.TopRunnableView?.MostFocused;
        var current = focused;
        while (current is not null)
        {
            var type = current.GetType();
            if (current is TextField || type.Name == "TextField" || type.FullName == "Terminal.Gui.Views.TextField")
            {
                return true;
            }

            current = current.SuperView;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _app.Keyboard.KeyDown -= OnGlobalKeyDown;
        _disposed = true;
    }
}

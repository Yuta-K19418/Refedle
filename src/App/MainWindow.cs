using Refedle.Engine.IO;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App;

/// <summary>
/// Main application window for Refedle TUI.
/// Owns the menu and status bar; orchestrates file loading
/// and content view management via <see cref="ViewManager"/>.
/// Members must be called on the UI thread, except <see cref="ScheduleStartupLoad"/>,
/// which defers its work through <c>_app.Invoke</c>.
/// </summary>
internal sealed class MainWindow : Window
{
    private readonly IApplication _app;
    private readonly AppState _state;
    private readonly IndexTaskManager _indexTaskManager = new();
    private readonly ViewManager _viewManager;
    private readonly AppKeyHandler _keyHandler;
    private readonly FileDialogHandler _fileDialogHandler;
    private readonly RecipeCommandHandler _recipeCommandHandler;
    private IRowIndexer? _activeIndexer;

    private Action<long, long>? _onProgressChanged;
    private Action? _onBuildIndexCompleted;
    private readonly IndexingProgressOverlay _indexingOverlay = new();

    public MainWindow(IApplication app, AppState state)
    {
        _app = app;
        _state = state;
        var modeController = new ModeController(state);

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        _viewManager = new ViewManager(this, state, modeController, app.Invoke);

        _fileDialogHandler = new FileDialogHandler(app, state, _viewManager, StartIndexing, StopCurrentIndexing);
        _recipeCommandHandler = new RecipeCommandHandler(app, state, _viewManager);

        InitializeMenu();
        InitializeStatusBar();
        _keyHandler = new AppKeyHandler(app, state, _viewManager, _fileDialogHandler, _recipeCommandHandler);
        _viewManager.SwitchToFileSelection();
    }

    /// <summary>
    /// Subscribes the global key handler to the application keyboard events.
    /// Should be called after Application.Init().
    /// </summary>
    internal void SubscribeKeyHandler()
    {
        _keyHandler.Subscribe();
    }

    private void InitializeMenu()
    {
        var openMenuItem = new MenuItem(
            "_Open", "", async () => await _fileDialogHandler.ShowAsync().ConfigureAwait(false));
        var saveRecipeMenuItem = new MenuItem(
            "_Save Recipe", "", async () => await _recipeCommandHandler.SaveAsync().ConfigureAwait(false));
        var loadRecipeMenuItem = new MenuItem(
            "_Load Recipe", "", async () => await _recipeCommandHandler.LoadAsync().ConfigureAwait(false));
        var exitMenuItem = new MenuItem("_Exit", "", () => _app.RequestStop());
        var fileMenuBarItem = new MenuBarItem("_File", [openMenuItem, saveRecipeMenuItem, loadRecipeMenuItem, exitMenuItem]);
        var menuBar = new MenuBar { Menus = [fileMenuBarItem] };

        Add(menuBar);
    }

    private void InitializeStatusBar()
    {
        var statusBar = new StatusBar
        {
            X = 0,
            // Place at the very last line of the window
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
        };

        _viewManager.RefreshStatusBarHints();
        Add(statusBar);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _indexingOverlay.Dispose();
            _keyHandler.Dispose();
            _indexTaskManager.Dispose();
            _state.Dispose();
            _viewManager.Dispose();
        }

        base.Dispose(disposing);
    }

    private void WireIndexerProgress(IRowIndexer indexer)
    {
        // Unsubscribe from the previous indexer to prevent event handler leaks
        // when a new file is opened while a previous indexer is still active.
        if (_activeIndexer is not null)
        {
            if (_onProgressChanged is not null)
            {
                _activeIndexer.ProgressChanged -= _onProgressChanged;
            }

            if (_onBuildIndexCompleted is not null)
            {
                _activeIndexer.BuildIndexCompleted -= _onBuildIndexCompleted;
            }
        }

        _indexingOverlay.Show(this);

        _onProgressChanged = OnProgressChanged;
        _onBuildIndexCompleted = OnBuildIndexCompleted;
        _activeIndexer = indexer;
        indexer.ProgressChanged += _onProgressChanged;
        indexer.BuildIndexCompleted += _onBuildIndexCompleted;

        _indexingOverlay.Update(indexer.BytesRead, indexer.FileSize);
    }

    private void OnProgressChanged(long bytesRead, long fileSize)
    {
        _app.Invoke(() => _indexingOverlay.Update(bytesRead, fileSize));
    }

    private void OnBuildIndexCompleted()
    {
        _app.Invoke(() =>
        {
            _indexingOverlay.Dismiss();
            _viewManager.RefreshStatusBarHints();
        });
    }

    /// <summary>
    /// Starts indexing and wires its progress events to the overlay.
    /// Must be called on the UI thread; it updates the indexing session state and Terminal.Gui views directly.
    /// </summary>
    internal void StartIndexing(IRowIndexer indexer)
    {
        WireIndexerProgress(indexer);
        _indexTaskManager.Start(indexer);
    }

    /// <summary>
    /// Stops the currently running indexing task and unwires progress events.
    /// Must be called on the UI thread; <see cref="IndexingProgressOverlay.Dismiss"/> modifies Terminal.Gui views.
    /// </summary>
    internal void StopCurrentIndexing()
    {
        if (_activeIndexer is not null)
        {
            if (_onProgressChanged is not null)
            {
                _activeIndexer.ProgressChanged -= _onProgressChanged;
            }

            if (_onBuildIndexCompleted is not null)
            {
                _activeIndexer.BuildIndexCompleted -= _onBuildIndexCompleted;
            }

            _activeIndexer = null;
            _onProgressChanged = null;
            _onBuildIndexCompleted = null;
        }

        _indexTaskManager.CancelCurrent();
        _indexingOverlay.Dismiss();
    }

    internal void ScheduleStartupLoad(TuiStartupOptions options)
    {
        if (options.InputFile is null)
        {
            return;
        }

        _app.Invoke(() => { _ = ExecuteStartupLoadAsync(options.InputFile, options.RecipeFile); });
    }

    private async Task ExecuteStartupLoadAsync(string inputFile, string? recipeFile)
    {
        // Continuation reads AppState; keep it on the UI thread before delegating to recipe I/O.
        await _fileDialogHandler.HandleFileSelectedAsync(inputFile).ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            return;
        }

        if (recipeFile is not null)
        {
            await _recipeCommandHandler.LoadFromPathAsync(recipeFile).ConfigureAwait(false);
        }
    }
}

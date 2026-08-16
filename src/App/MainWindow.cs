using System.Globalization;
using Refedle.Engine.IO;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App;

/// <summary>
/// Main application window for Refedle TUI.
/// Owns the menu and status bar; orchestrates file loading
/// and content view management via <see cref="ViewManager"/>.
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

    private ProgressBar? _progressBar;

    private Label? _progressLabel;

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
        var openMenuItem = new MenuItem("_Open", "", async () => await _fileDialogHandler.ShowAsync());
        var saveRecipeMenuItem = new MenuItem("_Save Recipe", "", async () => await _recipeCommandHandler.SaveAsync());
        var loadRecipeMenuItem = new MenuItem("_Load Recipe", "", async () => await _recipeCommandHandler.LoadAsync());
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

        ShowIndexingProgress();

        _onProgressChanged = OnProgressChanged;
        _onBuildIndexCompleted = OnBuildIndexCompleted;
        _activeIndexer = indexer;
        indexer.ProgressChanged += _onProgressChanged;
        indexer.BuildIndexCompleted += _onBuildIndexCompleted;

        UpdateIndexingProgress(indexer.BytesRead, indexer.FileSize);
    }

    private void OnProgressChanged(long bytesRead, long fileSize)
    {
        _app.Invoke(() => UpdateIndexingProgress(bytesRead, fileSize));
    }

    private void OnBuildIndexCompleted()
    {
        _app.Invoke(() =>
        {
            DismissIndexingProgress();
            _viewManager.RefreshStatusBarHints();
        });
    }

    internal void StartIndexing(IRowIndexer indexer)
    {
        WireIndexerProgress(indexer);
        _indexTaskManager.Start(indexer);
    }

    /// <summary>
    /// Stops the currently running indexing task and unwires progress events.
    /// Must be called on the UI thread; <see cref="DismissIndexingProgress"/> modifies Terminal.Gui views.
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
        DismissIndexingProgress();
    }

    private void ShowIndexingProgress()
    {
        DismissIndexingProgress();
        _progressBar = new ProgressBar
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = Dim.Percent(60),
        };
        _progressLabel = new Label
        {
            X = Pos.Center(),
            Y = Pos.Bottom(_progressBar) + 1,
            Text = "Indexing…",
        };

        Add(_progressBar, _progressLabel);
    }

    private void UpdateIndexingProgress(long bytesRead, long fileSize)
    {
        if (_progressBar is null || _progressLabel is null)
        {
            return;
        }

        if (fileSize <= 0)
        {
            return;
        }

        var fraction = (float)bytesRead / fileSize;
        _progressBar.Fraction = fraction;
        _progressLabel.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"Indexing… {fraction * 100:F0}%  ({FormatBytes(bytesRead)} / {FormatBytes(fileSize)})");
    }

    private void DismissIndexingProgress()
    {
        if (_progressBar is not null)
        {
            Remove(_progressBar);
            _progressBar.Dispose();
            _progressBar = null;
        }

        if (_progressLabel is not null)
        {
            Remove(_progressLabel);
            _progressLabel.Dispose();
            _progressLabel = null;
        }
    }

    private static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            >= GB => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)GB:F2} GB"),
            >= MB => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)MB:F2} MB"),
            >= KB => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)KB:F2} KB"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
        };
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
        await _fileDialogHandler.HandleFileSelectedAsync(inputFile);

        if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            return;
        }

        if (recipeFile is not null)
        {
            await _recipeCommandHandler.LoadFromPathAsync(recipeFile);
        }
    }
}

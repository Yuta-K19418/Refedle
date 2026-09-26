using Refedle.App.Tui.Workers;
using Refedle.App.Tui.Workers.Schema;
using Refedle.Engine.IO;
using Refedle.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui;

/// <summary>
/// Handles file dialog operations for opening data files.
/// </summary>
internal sealed class FileDialogHandler(
    IApplication app,
    AppState state,
    ViewManager viewManager,
    Action<IRowIndexer> onIndexerStart,
    Action stopIndexing,
    Func<string, ISchemaScanner> scannerFactory,
    Action<string> onFilePathChanged)
{
    private readonly IApplication _app = app;
    private readonly AppState _state = state;
    private readonly ViewManager _viewManager = viewManager;
    private readonly Action<IRowIndexer> _onIndexerStart = onIndexerStart;
    private readonly Action _stopIndexing = stopIndexing;
    private readonly Func<string, ISchemaScanner> _scannerFactory =
        scannerFactory ?? throw new ArgumentNullException(nameof(scannerFactory));
    private readonly Action<string> _onFilePathChanged =
        onFilePathChanged ?? throw new ArgumentNullException(nameof(onFilePathChanged));

    internal async Task ShowAsync()
    {
        var dialog = new OpenDialog { Title = "Open File" };
        dialog.AllowedTypes.Add(new AllowedType("CSV file", ".csv"));
        dialog.AllowedTypes.Add(new AllowedType("JSON file", ".json"));
        dialog.AllowedTypes.Add(new AllowedType("JSON Lines file", ".jsonl"));

        // Modal dialog: the continuation feeds dialog.Path into HandleFileSelectedAsync, whose
        // prologue mutates AppState without app.Invoke, so it must stay on the UI thread.
        await _app.RunAsync(dialog, _state.Cts.Token, errorHandler: null).ConfigureAwait(true);

        if (dialog.Canceled || string.IsNullOrEmpty(dialog.Path))
        {
            return;
        }

        await HandleFileSelectedAsync(dialog.Path).ConfigureAwait(false);
    }

    internal async Task HandleFileSelectedAsync(string path)
    {
        var detectionResult = FormatDetector.DetectInputFile(path);
        if (detectionResult.IsFailure)
        {
            _viewManager.ShowError(detectionResult.Error);
            return;
        }

        var format = detectionResult.Value;

        // Reset state for new file
        _state.CurrentFilePath = path;
        _app.Invoke(() => _onFilePathChanged(path));
        _state.ClearMorphActions();
        // The fresh session starts clean: an empty stack cannot diverge from anything on disk.
        _state.MarkRecipeSaved();
        _state.RenewCtsWithCancel();
        _state.DrillDown = null;
        _state.JsonObjectEntries = null;

        // JSON Object: scan keys via TopLevelScanner, then switch to tree view directly.
        // No IRowIndexer is needed — keys are not rows.
        if (format == DataFormat.JsonObject)
        {
            await LoadJsonObjectAsync(path).ConfigureAwait(false);
            return;
        }

        // Create indexer from factory
        var indexer = RowIndexerFactory.Create(format, path);

        // SwitchToView(format)
        if (format == DataFormat.Csv)
        {
            await LoadCsvAsync(path, indexer).ConfigureAwait(false);
            return;
        }

        if (format == DataFormat.JsonLines)
        {
            await LoadJsonLinesAsync(indexer).ConfigureAwait(false);
            return;
        }

        if (format == DataFormat.JsonArray)
        {
            await LoadJsonArrayAsync(indexer).ConfigureAwait(false);
            return;
        }

        _onIndexerStart(indexer);
    }

    private async Task LoadJsonObjectAsync(string path)
    {
        _stopIndexing();
        _state.RowIndexer = null;
        _state.Schema = null;
        _state.OnSchemaRefined = null;

        var ct = _state.Cts.Token;
        try
        {
            var entries = await Task.Run(
                () => Engine.IO.JsonObject.TopLevelScanner.Scan(path, ct), ct).ConfigureAwait(false);
            _app.Invoke(() =>
            {
                _state.CurrentMode = ViewMode.JsonObjectTree;
                _state.JsonObjectEntries = entries;
                _viewManager.SwitchToJsonObjectTree(entries);
            });
        }
        catch (OperationCanceledException) { /* file reloaded before scan completed */ }
        catch (Exception ex)
        {
            _app.Invoke(() =>
                _viewManager.ShowError($"Error loading JSON Object: {ex.Message}"));
        }
    }

    private async Task LoadCsvAsync(string path, IRowIndexer indexer)
    {
        var schemaScanner = _scannerFactory(path);
        // Capture by value before the scans: RenewCtsWithCancel cancels the CTS when
        // another file loads, and the staleness check needs the token from scan start.
        var scanToken = _state.Cts.Token;
        try
        {
            var schema = await schemaScanner.InitialScanAsync().ConfigureAwait(false);

            // Skip UI posts for an already-cancelled scan; the posted callback re-checks
            // the token because the session can be replaced between posting and execution.
            if (scanToken.IsCancellationRequested)
            {
                return;
            }

            _app.Invoke(() =>
            {
                // Re-check the token on the UI thread: the session can be replaced after posting.
                if (scanToken.IsCancellationRequested)
                {
                    return;
                }

                if (schema.Columns.Count == 0)
                {
                    _viewManager.ShowError("File contains no data");
                    return;
                }

                _state.Schema = schema;
                _state.RowIndexer = indexer;
                _state.CurrentMode = ViewMode.CsvTable;

                _viewManager.SwitchToCsvTable(indexer, schema);

                _ = BackgroundSchemaRefiner.StartAsync(
                    _state, schemaScanner, schema, _app.Invoke, scanToken);

                _onIndexerStart(indexer);
            });
        }
        catch (Exception ex)
        {
            // Suppress errors from a scan whose session was already replaced; the posted
            // callback re-checks the token so a switch between posting and execution is also covered.
            if (scanToken.IsCancellationRequested)
            {
                return;
            }

            _app.Invoke(() =>
            {
                // Re-check the token on the UI thread: the session can be replaced after posting.
                if (scanToken.IsCancellationRequested)
                {
                    return;
                }

                _viewManager.ShowError($"Error scanning CSV: {ex.Message}");
            });
        }
    }

    private async Task LoadJsonLinesAsync(IRowIndexer indexer)
    {
        try
        {
            _state.RowIndexer = indexer;
            _state.Schema = null;
            _state.OnSchemaRefined = null;

            var tcs = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            indexer.FirstCheckpointReached += () => tcs.TrySetResult();

            _onIndexerStart(indexer);
            await tcs.Task.ConfigureAwait(false);

            _app.Invoke(() =>
            {
                _state.CurrentMode = ViewMode.JsonLinesTree;
                _viewManager.SwitchToJsonLinesTree(indexer);
            });
        }
        catch (Exception ex)
        {
            _app.Invoke(() => _viewManager.ShowError($"Error loading JSON Lines: {ex.Message}"));
        }
    }

    private async Task LoadJsonArrayAsync(IRowIndexer indexer)
    {
        try
        {
            _state.RowIndexer = indexer;
            _state.Schema = null;
            _state.OnSchemaRefined = null;

            var tcs = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            indexer.FirstCheckpointReached += () => tcs.TrySetResult();

            _onIndexerStart(indexer);
            await tcs.Task.ConfigureAwait(false);

            _app.Invoke(() =>
            {
                _state.CurrentMode = ViewMode.JsonArrayTree;
                _viewManager.SwitchToJsonArrayTree(indexer);
            });
        }
        catch (Exception ex)
        {
            _app.Invoke(() => _viewManager.ShowError($"Error loading JSON Array: {ex.Message}"));
        }
    }
}

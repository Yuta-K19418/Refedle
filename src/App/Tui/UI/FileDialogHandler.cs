using Refedle.App.Tui.Workers;
using Refedle.App.Tui.Workers.Schema;
using Refedle.Engine;
using Refedle.Engine.IO;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI;

/// <summary>
/// Handles file dialog operations for opening data files.
/// </summary>
internal sealed class FileDialogHandler
{
    private readonly IApplication _app;
    private readonly AppState _state;
    private readonly ViewManager _viewManager;
    private readonly Action<IRowIndexer> _onIndexerStart;
    private readonly Action _stopIndexing;
    private readonly Func<string, ISchemaScanner> _scannerFactory;
    private readonly Action<string> _onFilePathChanged;

    internal FileDialogHandler(
        IApplication app,
        AppState state,
        ViewManager viewManager,
        Action<IRowIndexer> onIndexerStart,
        Action stopIndexing,
        Func<string, ISchemaScanner> scannerFactory,
        Action<string> onFilePathChanged)
    {
        ArgumentNullException.ThrowIfNull(scannerFactory);
        ArgumentNullException.ThrowIfNull(onFilePathChanged);

        _app = app;
        _state = state;
        _viewManager = viewManager;
        _onIndexerStart = onIndexerStart;
        _stopIndexing = stopIndexing;
        _scannerFactory = scannerFactory;
        _onFilePathChanged = onFilePathChanged;
    }

    internal async Task ShowAsync()
    {
        OpenDialog dialog = new() { Title = "Open File" };
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
        Result<DataFormat> detectionResult = FormatDetector.DetectInputFile(path);
        if (detectionResult.IsFailure)
        {
            _viewManager.ShowError(detectionResult.Error);
            return;
        }

        DataFormat format = detectionResult.Value;

        // Reset state for new file
        _state.StartNewFile(path);
        _app.Invoke(() => _onFilePathChanged(path));

        // JSON Object: scan keys via TopLevelScanner, then switch to tree view directly.
        // No IRowIndexer is needed — keys are not rows.
        if (format == DataFormat.JsonObject)
        {
            await LoadJsonObjectAsync(path).ConfigureAwait(false);
            return;
        }

        // Create indexer from factory
        IRowIndexer indexer = RowIndexerFactory.Create(format, path);

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
        _state.BeginJsonObjectLoad();

        CancellationToken ct = _state.Cts.Token;
        try
        {
            IReadOnlyList<JsonObjectEntry> entries = await JsonObjectScanRunner.RunAsync(path, ct).ConfigureAwait(false);
            _app.Invoke(() =>
            {
                _state.EnterJsonObjectTree(entries);
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
        ISchemaScanner schemaScanner = _scannerFactory(path);
        // Capture by value before the scans: RenewCtsWithCancel cancels the CTS when
        // another file loads, and the staleness check needs the token from scan start.
        CancellationToken scanToken = _state.Cts.Token;
        try
        {
            TableSchema schema = await schemaScanner.InitialScanAsync().ConfigureAwait(false);

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

                _state.CompleteCsvLoad(indexer, schema);

                _viewManager.SwitchToCsvTable(indexer, schema);

                Task indexingCompleted = IndexingCompletion.WhenCompletedAsync(indexer);
                _ = BackgroundSchemaRefiner.StartAsync(
                    _state, schemaScanner, schema, _app.Invoke, indexingCompleted, scanToken);

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
            _state.BeginIndexedTreeLoad(indexer);

            TaskCompletionSource tcs = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            indexer.FirstCheckpointReached += () => tcs.TrySetResult();

            _onIndexerStart(indexer);
            await tcs.Task.ConfigureAwait(false);

            _app.Invoke(() =>
            {
                _state.EnterJsonLinesTree();
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
            _state.BeginIndexedTreeLoad(indexer);

            TaskCompletionSource tcs = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            indexer.FirstCheckpointReached += () => tcs.TrySetResult();

            _onIndexerStart(indexer);
            await tcs.Task.ConfigureAwait(false);

            _app.Invoke(() =>
            {
                _state.EnterJsonArrayTree();
                _viewManager.SwitchToJsonArrayTree(indexer);
            });
        }
        catch (Exception ex)
        {
            _app.Invoke(() => _viewManager.ShowError($"Error loading JSON Array: {ex.Message}"));
        }
    }
}

using System.Globalization;
using Refedle.App.Schema;
using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.App;

/// <summary>
/// Orchestrates view mode transitions and associated lazy initialization logic.
/// </summary>
internal sealed class ModeController(
    AppState state,
    Action<Action> uiThreadInvoke,
    Func<string, ISchemaScanner> scannerFactory)
{
    private readonly AppState _state = state ?? throw new ArgumentNullException(nameof(state));
    private readonly Action<Action> _uiThreadInvoke =
        uiThreadInvoke ?? throw new ArgumentNullException(nameof(uiThreadInvoke));
    private readonly Func<string, ISchemaScanner> _scannerFactory =
        scannerFactory ?? throw new ArgumentNullException(nameof(scannerFactory));

    /// <summary>
    /// Toggles the JSON Lines display mode between Tree and Table.
    /// Performs a lazy schema scan on the first switch to Table mode.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating success or the reason for failure.</returns>
    public async ValueTask<Result> ToggleJsonLinesModeAsync()
    {
        if (_state.CurrentMode == ViewMode.JsonLinesTable)
        {
            _state.CurrentMode = ViewMode.JsonLinesTree;
            return Results.Success();
        }

        if (_state.CurrentMode != ViewMode.JsonLinesTree)
        {
            return Results.Success();
        }

        if (_state.RowIndexer is null)
        {
            return Results.Success();
        }

        // Subsequent switch: reuse cached schema
        if (_state.Schema is not null)
        {
            _state.CurrentMode = ViewMode.JsonLinesTable;
            return Results.Success();
        }

        // First switch: scan schema lazily
        if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            return Results.Failure("No file is currently open");
        }

        var scanner = _scannerFactory(_state.CurrentFilePath);
        // Capture by value before the fire-and-forget scan: StartBackgroundScanAsync
        // reports cancellation as success, so the guards must detect that the CTS was
        // renewed while the scan was still running.
        var scanToken = _state.Cts.Token;

        try
        {
            var schema = await scanner.InitialScanAsync().ConfigureAwait(true);

            // Skip state writes for an already-cancelled scan: RenewCtsWithCancel always
            // cancels the token when a session is replaced.
            if (scanToken.IsCancellationRequested)
            {
                return Results.Success();
            }

            _state.Schema = schema;
            _state.CurrentMode = ViewMode.JsonLinesTable;

            _ = BackgroundSchemaRefiner.StartAsync(_state, scanner, schema, _uiThreadInvoke, scanToken);

            return Results.Success();
        }
        catch (InvalidOperationException ex)
        {
            return ToScanFailure(ex.Message, scanToken);
        }
        catch (InvalidDataException ex)
        {
            return ToScanFailure($"Invalid JSON Lines format: {ex.Message}", scanToken);
        }
    }

    /// <summary>
    /// Converts an initial-scan failure into a result, suppressing the error when the scan
    /// belonged to a cancelled (replaced) file session so the replacement session never sees
    /// a stale error. Token-only: the failure is evaluated off the UI thread.
    /// </summary>
    /// <param name="error">The scan error message.</param>
    /// <param name="scanToken">The token captured when the scan started.</param>
    /// <returns>A suppressed success for a stale session, otherwise the failure.</returns>
    private static Result ToScanFailure(string error, CancellationToken scanToken)
    {
        return scanToken.IsCancellationRequested ? Results.Success() : Results.Failure(error);
    }

    /// <summary>
    /// Executes the Single DrillDown command for the given request.
    /// Parses the selected node's bytes in memory, infers schema, and stores results in AppState.
    /// </summary>
    /// <param name="request">The DrillDown request carrying the selected node bytes and context.</param>
    /// <returns>A <see cref="Result"/> indicating success or the reason for failure.</returns>
    public Result DrillDown(SingleDrillDownRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = DrillDownSchemaExtractor.ExtractFromNode(request.NodeBytes, request.Format);
        if (result.IsFailure)
        {
            return Results.Failure(result.Error);
        }

        var children = result.Value.childRawValues;
        var rows = new FocusedTableRow[children.Count];
        for (var i = 0; i < children.Count; i++)
        {
            rows[i] = new FocusedTableRow(children[i], string.Create(CultureInfo.InvariantCulture, $"[{i}]"));
        }

        _state.DrillDown = new DrillDownState(rows, result.Value.schema, _state.CurrentMode, request.KeyPath, ActionStack: request.InitialActionStack);
        _state.CurrentMode = ViewMode.FocusedTable;

        return Results.Success();
    }

    /// <summary>
    /// Executes the Full Aggregation DrillDown file scan on a background thread and returns the result.
    /// Does not mutate AppState — the caller is responsible for applying the result on the UI thread.
    /// </summary>
    /// <param name="request">The full-aggregation DrillDown request carrying the KeyPath.</param>
    /// <returns>A <see cref="Result{T}"/> containing the scanned <see cref="DrillDownState"/> on success.</returns>
    public async ValueTask<Result<DrillDownState>> FullAggregationDrillDownAsync(FullAggregationDrillDownRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The caller may be off the UI thread (recipe replay), so _state is read on the UI thread
        // and captured by value before Task.Run.
        var captured = new TaskCompletionSource<(string filePath, CancellationToken ct, ViewMode previousMode)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _uiThreadInvoke(() =>
            captured.SetResult((_state.CurrentFilePath, _state.Cts.Token, _state.CurrentMode)));
        var (filePath, ct, previousMode) = await captured.Task.ConfigureAwait(false);

        var result = await Task.Run(
            () => FullAggregationScanner.Scan(filePath, request.Format, request.KeyPath, ct),
            ct).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Results.Failure<DrillDownState>(result.Error);
        }

        return Results.Success(new DrillDownState(result.Value.rows, result.Value.schema, previousMode, request.KeyPath, ActionStack: request.InitialActionStack));
    }
}

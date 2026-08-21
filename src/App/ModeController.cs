using System.Globalization;
using Refedle.App.Schema.JsonLines;
using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.App;

/// <summary>
/// Orchestrates view mode transitions and associated lazy initialization logic.
/// </summary>
internal sealed class ModeController
{
    private readonly AppState _state;

    public ModeController(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

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

        var scanner = new IncrementalSchemaScanner(_state.CurrentFilePath);

        try
        {
            var schema = await scanner.InitialScanAsync();
            _state.Schema = schema;
            _state.CurrentMode = ViewMode.JsonLinesTable;

            _ = scanner
                .StartBackgroundScanAsync(schema, _state.Cts.Token)
                .ContinueWith(
                    t =>
                    {
                        if (!t.IsCompletedSuccessfully)
                        {
                            return;
                        }

                        _state.Schema = t.Result;
                        _state.OnSchemaRefined?.Invoke(t.Result);
                    },
                    TaskScheduler.Default
                );

            return Results.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Failure(ex.Message);
        }
        catch (InvalidDataException ex)
        {
            return Results.Failure($"Invalid JSON Lines format: {ex.Message}");
        }
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

        _state.DrillDown = new DrillDownState(rows, result.Value.schema, _state.CurrentMode, ActionStack: []);
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

        // Capture by value before Task.Run to avoid reading _state on the thread-pool thread.
        var filePath = _state.CurrentFilePath;
        var ct = _state.Cts.Token;
        var previousMode = _state.CurrentMode;

        var result = await Task.Run(
            () => FullAggregationScanner.Scan(filePath, request.Format, request.KeyPath, ct),
            ct);

        if (result.IsFailure)
        {
            return Results.Failure<DrillDownState>(result.Error);
        }

        return Results.Success(new DrillDownState(result.Value.rows, result.Value.schema, previousMode, ActionStack: []));
    }
}

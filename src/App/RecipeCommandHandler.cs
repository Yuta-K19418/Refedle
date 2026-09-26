using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Recipes;
using Refedle.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Refedle.App;

/// <summary>
/// Handles recipe save and load operations.
/// </summary>
internal sealed class RecipeCommandHandler(
    IApplication app,
    AppState state,
    ViewManager viewManager,
    IRecipeManager? recipeManager = null)
{
    private readonly IApplication _app = app;
    private readonly AppState _state = state;
    private readonly ViewManager _viewManager = viewManager;
    private readonly IRecipeManager _recipeManager = recipeManager ?? new RecipeManager();

    /// <summary>
    /// Saves the current recipe. Threading: all AppState access is UI-thread-only, so the capture of
    /// the DrillDown scope, <see cref="AppState.Revision"/> and recipe (before the write) and the
    /// completion continuation (after it, via <c>IApplication.Invoke</c>) both run on the UI thread;
    /// no lock is needed and none is provided.
    /// </summary>
    internal async Task SaveAsync()
    {
        if (_state.CurrentMode is not (ViewMode.CsvTable or ViewMode.JsonLinesTable or ViewMode.JsonLinesTree or ViewMode.FocusedTable))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            return;
        }

        var dialog = new OpenDialog { Title = "Save Recipe" };
        dialog.AllowedTypes.Add(new AllowedType("YAML file", ".yaml"));

        // Modal dialog: the continuation reads dialog result and AppState (BuildRecipe) on the UI thread.
        await _app.RunAsync(dialog, _state.Cts.Token, errorHandler: null).ConfigureAwait(true);

        if (dialog.Canceled || string.IsNullOrEmpty(dialog.Path))
        {
            return;
        }

        // Captured with BuildRecipe (all on the UI thread) so the flag cleared on success matches
        // the scope and version that were saved, not whatever is current when the write completes.
        var savedDrillDown = CaptureSavedDrillDownScope();
        var savedRevision = _state.Revision;
        var recipe = BuildRecipe();

        var result = await _recipeManager.SaveAsync(recipe, dialog.Path, _state.Cts.Token).ConfigureAwait(false);

        _app.Invoke(() =>
        {
            if (result.IsFailure)
            {
                _viewManager.ShowError(result.Error);
                return;
            }

            MarkScopeSaved(savedDrillDown, savedRevision);
            MessageBox.Query(_app, "Save Recipe", "Recipe saved successfully.", "OK");
        });
    }

    /// <summary>
    /// Captures the DrillDown session a save from FocusedTable mode covers, or <c>null</c> when the
    /// root Action Stack is the scope being saved — mirroring the scope rule in
    /// <see cref="BuildRecipe"/> so the two never drift apart.
    /// </summary>
    private DrillDownState? CaptureSavedDrillDownScope() =>
        _state.IsDrillDownMode && _state.TryGetDrillDown(out var scope) ? scope : null;

    /// <summary>
    /// Clears the unsaved-changes flag of the scope that was just saved: the DrillDown session when
    /// the recipe captured it, the root Action Stack otherwise. The reference check (DrillDown) and
    /// the revision check (root) keep edits made during the async write from being marked saved.
    /// </summary>
    private void MarkScopeSaved(DrillDownState? savedDrillDown, long savedRevision)
    {
        if (savedDrillDown is null)
        {
            _state.MarkRecipeSavedIfUnchanged(savedRevision);
            return;
        }

        if (_state.TryGetDrillDown(out var current) && ReferenceEquals(current, savedDrillDown))
        {
            _state.MarkDrillDownSaved();
        }
    }

    /// <summary>
    /// Builds the Recipe to save under the Save Scope rule: a FocusedTable view with an active
    /// DrillDown captures only that DrillDown's scope (KeyPath + ActionStack), never the base
    /// table's AppState.ActionStack. Any other mode — including a stale AppState.DrillDown left
    /// over from navigating back without clearing it — captures the base table's ActionStack,
    /// with DrillDownKeyPath left unset.
    /// </summary>
    internal Recipe BuildRecipe() =>
        _state.IsDrillDownMode && _state.TryGetDrillDown(out var drillDown)
            ? new Recipe
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(_state.CurrentFilePath),
                Actions = drillDown.ActionStack,
                DrillDownKeyPath = drillDown.KeyPath,
                LastModified = System.DateTimeOffset.UtcNow,
            }
            : new Recipe
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(_state.CurrentFilePath),
                Actions = _state.ActionStack,
                LastModified = System.DateTimeOffset.UtcNow,
            };

    internal async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            return;
        }

        var dialog = new OpenDialog { Title = "Load Recipe" };
        dialog.AllowedTypes.Add(new AllowedType("YAML file", ".yaml"));

        // Modal dialog: the continuation reads dialog result and AppState (LoadFromPathAsync prologue) on the UI thread.
        await _app.RunAsync(dialog, _state.Cts.Token, errorHandler: null).ConfigureAwait(true);

        if (dialog.Canceled || string.IsNullOrEmpty(dialog.Path))
        {
            return;
        }

        await LoadFromPathAsync(dialog.Path).ConfigureAwait(false);
    }

    internal async ValueTask LoadFromPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            throw new InvalidOperationException("Cannot load recipe: no input file is currently loaded.");
        }

        // Captured before the await so the DrillDown branch never reads AppState off the UI thread.
        var currentFilePath = _state.CurrentFilePath;
        var result = await _recipeManager.LoadAsync(path, _state.Cts.Token).ConfigureAwait(false);

        if (result.IsFailure)
        {
            _app.Invoke(() => _viewManager.ShowError(result.Error));
            return;
        }

        var recipe = result.Value;
        if (recipe.DrillDownKeyPath is not { } keyPath)
        {
            _app.Invoke(() =>
            {
                _state.SetActionStack(recipe.Actions);
                // Loading counts as saved: the stack now mirrors the recipe file on disk.
                _state.MarkRecipeSaved();
                _viewManager.RefreshCurrentTableView();
            });
            return;
        }

        await LoadDrillDownRecipeAsync(recipe, keyPath, currentFilePath).ConfigureAwait(false);
    }

    /// <summary>
    /// Replays a recipe's recorded DrillDown location: Full Aggregation DrillDown (JSON Lines/Array)
    /// re-scans the whole file by KeyPath; Single DrillDown (JSON Object) resolves the recorded path
    /// against the cached top-level entries. Both request types carry <c>recipe.Actions</c> as their
    /// <c>InitialActionStack</c>, so the resulting <see cref="DrillDownState"/> — and the
    /// FocusedTableTransformer built from it — reflect the recipe's actions from the very first
    /// render, instead of a separate post-hoc patch that a failed transition could leave inconsistent.
    /// </summary>
    private async ValueTask LoadDrillDownRecipeAsync(
        Recipe recipe, IReadOnlyList<KeyPathSegment> keyPath, string currentFilePath)
    {
        var formatResult = FormatDetector.DetectInputFile(currentFilePath);
        if (formatResult.IsFailure)
        {
            _app.Invoke(() => _viewManager.ShowError(formatResult.Error));
            return;
        }

        var format = formatResult.Value;
        if (format == DataFormat.JsonObject)
        {
            _app.Invoke(() => LoadSingleDrillDownRecipe(recipe, format, keyPath));
            return;
        }

        if (format is not (DataFormat.JsonLines or DataFormat.JsonArray))
        {
            _app.Invoke(() => _viewManager.ShowError(
                $"This recipe's DrillDown path cannot be replayed against a {format} file."));
            return;
        }

        var request = new FullAggregationDrillDownRequest(format, keyPath, recipe.Actions);
        await _viewManager.FullAggregationDrillDownAsync(request).ConfigureAwait(false);
    }

    private void LoadSingleDrillDownRecipe(Recipe recipe, DataFormat format, IReadOnlyList<KeyPathSegment> keyPath)
    {
        if (keyPath.Count == 0)
        {
            _viewManager.ShowError("This recipe's DrillDown path is empty, which is not valid for a JSON Object file.");
            return;
        }

        var entryResult = FindRootEntry(keyPath[0].Value);
        if (entryResult.IsFailure)
        {
            _viewManager.ShowError(entryResult.Error);
            return;
        }

        IReadOnlyList<KeyPathSegment> remainingKeyPath = [.. keyPath.Skip(1)];
        var nodeResult = KeyPathNodeResolver.ResolveSingleNode(entryResult.Value.Value, remainingKeyPath);
        if (nodeResult.IsFailure)
        {
            _viewManager.ShowError(nodeResult.Error);
            return;
        }

        var request = new SingleDrillDownRequest(format, nodeResult.Value, keyPath, recipe.Actions);
        _viewManager.DrillDown(request);
    }

    private Result<JsonObjectEntry> FindRootEntry(string key)
    {
        foreach (var entry in _state.JsonObjectEntries ?? [])
        {
            if (entry.Key == key)
            {
                return Results.Success(entry);
            }
        }

        return Results.Failure<JsonObjectEntry>(
            $"DrillDown path key \"{key}\" was not found in this file's top-level entries.");
    }
}

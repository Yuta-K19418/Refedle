using Refedle.Engine.Models;
using Refedle.Engine.Recipes;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Refedle.App;

/// <summary>
/// Handles recipe save and load operations.
/// </summary>
internal sealed class RecipeCommandHandler(
    IApplication app,
    AppState state,
    ViewManager viewManager)
{
    private readonly IApplication _app = app;
    private readonly AppState _state = state;
    private readonly ViewManager _viewManager = viewManager;
    private readonly RecipeManager _recipeManager = new();

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

        await _app.RunAsync(dialog, _state.Cts.Token, errorHandler: null);

        if (dialog.Canceled || string.IsNullOrEmpty(dialog.Path))
        {
            return;
        }

        var recipe = BuildRecipe();

        var result = await _recipeManager.SaveAsync(recipe, dialog.Path, _state.Cts.Token);

        _app.Invoke(() =>
        {
            if (result.IsFailure)
            {
                _viewManager.ShowError(result.Error);
                return;
            }

            MessageBox.Query(_app, "Save Recipe", "Recipe saved successfully.", "OK");
        });
    }

    /// <summary>
    /// Builds the Recipe to save under the Save Scope rule: a FocusedTable view with an active
    /// DrillDown captures only that DrillDown's scope (KeyPath + ActionStack), never the base
    /// table's AppState.ActionStack. Any other mode — including a stale AppState.DrillDown left
    /// over from navigating back without clearing it — captures the base table's ActionStack,
    /// with DrillDownKeyPath left unset.
    /// </summary>
    internal Recipe BuildRecipe() =>
        _state.CurrentMode == ViewMode.FocusedTable && _state.DrillDown is { } drillDown
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

        await _app.RunAsync(dialog, _state.Cts.Token, errorHandler: null);

        if (dialog.Canceled || string.IsNullOrEmpty(dialog.Path))
        {
            return;
        }

        await LoadFromPathAsync(dialog.Path);
    }

    internal async ValueTask LoadFromPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            throw new InvalidOperationException("Cannot load recipe: no input file is currently loaded.");
        }

        var result = await _recipeManager.LoadAsync(path, _state.Cts.Token);

        _app.Invoke(() =>
        {
            if (result.IsFailure)
            {
                _viewManager.ShowError(result.Error);
                return;
            }

            _state.SetActionStack(result.Value.Actions);
            _viewManager.RefreshCurrentTableView();
        });
    }
}

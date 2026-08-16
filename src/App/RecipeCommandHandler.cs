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
        if (_state.CurrentMode is not (ViewMode.CsvTable or ViewMode.JsonLinesTable or ViewMode.JsonLinesTree))
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

        var recipe = new Recipe
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(_state.CurrentFilePath),
            Actions = _state.ActionStack,
            LastModified = System.DateTimeOffset.UtcNow,
        };

        var result = await _recipeManager.SaveAsync(recipe, dialog.Path);

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

        var result = await _recipeManager.LoadAsync(path);

        _app.Invoke(() =>
        {
            if (result.IsFailure)
            {
                _viewManager.ShowError(result.Error);
                return;
            }

            _state.ActionStack = result.Value.Actions;
            _viewManager.RefreshCurrentTableView();
        });
    }
}

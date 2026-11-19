using AwesomeAssertions;
using Refedle.Engine;
using Refedle.Engine.Models;
using Refedle.Engine.Recipes;
using Refedle.TestHelpers.IO;

namespace Refedle.IntegrationTests.App.Tui.UI;

public sealed partial class RecipeCommandHandlerTests : IDisposable
{
    private readonly TempFileCollection _tempFiles = new();

    private readonly string _jsonObjectFile;

    private readonly string _jsonLinesFile;

    private readonly string _csvFile;

    private readonly string _recipeFile;

    public RecipeCommandHandlerTests()
    {
        _jsonObjectFile = _tempFiles.ReservePath(".json");
        _jsonLinesFile = _tempFiles.ReservePath(".jsonl");
        _csvFile = _tempFiles.ReservePath(".csv");
        _recipeFile = _tempFiles.ReservePath(".yaml");
    }

    public void Dispose() => _tempFiles.Dispose();

    private static async Task SaveRecipeAsync(Recipe recipe, string path)
    {
        Result saveResult = await new RecipeManager().SaveAsync(recipe, path);
        saveResult.IsSuccess.Should().BeTrue();
    }
}

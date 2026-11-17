using AwesomeAssertions;
using Refedle.Engine;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Recipes;

namespace Refedle.Tests.Engine.Recipes;

public sealed class RecipeManagerTests : IDisposable
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), $"recipe_tests_{Guid.NewGuid()}");

    public RecipeManagerTests()
    {
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTrip_ReturnsEquivalentRecipe()
    {
        // Arrange
        RecipeManager manager = new();
        string filePath = Path.Combine(_testDir, "recipe.yaml");
        Recipe recipe = new()
        {
            Name = "test",
            Description = "Test recipe",
            LastModified = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero),
            Actions = [new RenameColumnAction { OldName = "old", NewName = "new" }],
        };

        // Act
        Result saveResult = await manager.SaveAsync(recipe, filePath);
        Result<Recipe> loadResult = await manager.LoadAsync(filePath);

        // Assert
        saveResult.IsSuccess.Should().BeTrue();
        loadResult.IsSuccess.Should().BeTrue();
        loadResult.Value.Name.Should().Be("test");
        loadResult.Value.Description.Should().Be("Test recipe");
        loadResult.Value.LastModified.Should().Be(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero));
        loadResult.Value.Actions.Should().HaveCount(1);
        RenameColumnAction action = loadResult.Value.Actions[0].Should().BeOfType<RenameColumnAction>().Subject;
        action.OldName.Should().Be("old");
        action.NewName.Should().Be("new");
    }

    [Fact]
    public async Task SaveAsync_CreatesFileAtSpecifiedPath()
    {
        // Arrange
        RecipeManager manager = new();
        string filePath = Path.Combine(_testDir, "recipe.yaml");
        Recipe recipe = new() { Name = "test", Actions = [] };

        // Act
        Result result = await manager.SaveAsync(recipe, filePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WritesFileWithoutUtf8Bom()
    {
        // Arrange
        RecipeManager manager = new();
        string filePath = Path.Combine(_testDir, "recipe.yaml");
        Recipe recipe = new() { Name = "test", Actions = [] };

        // Act
        Result result = await manager.SaveAsync(recipe, filePath);

        // Assert — check the BOM bytes literally so the test does not depend on the input-side utility.
        result.IsSuccess.Should().BeTrue();
        byte[] utf8Bom = [0xEF, 0xBB, 0xBF];
        File.ReadAllBytes(filePath).AsSpan(0, 3).SequenceEqual(utf8Bom).Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingFile()
    {
        // Arrange
        RecipeManager manager = new();
        string filePath = Path.Combine(_testDir, "recipe.yaml");
        await File.WriteAllTextAsync(filePath, "existing content");
        Recipe recipe = new() { Name = "overwritten", Actions = [] };

        // Act
        Result saveResult = await manager.SaveAsync(recipe, filePath);
        Result<Recipe> loadResult = await manager.LoadAsync(filePath);

        // Assert
        saveResult.IsSuccess.Should().BeTrue();
        loadResult.IsSuccess.Should().BeTrue();
        loadResult.Value.Name.Should().Be("overwritten");
    }

    [Fact]
    public async Task SaveAsync_WritesUtf8WithoutBom()
    {
        // Arrange
        RecipeManager manager = new();
        string filePath = Path.Combine(_testDir, "recipe.yaml");
        Recipe recipe = new() { Name = "test", Actions = [] };

        // Act
        await manager.SaveAsync(recipe, filePath);
        byte[] bytes = await File.ReadAllBytesAsync(filePath);

        // Assert
        // UTF-8 BOM starts with 0xEF 0xBB 0xBF; absence of 0xEF confirms no BOM is written
        bytes[0].Should().NotBe(0xEF);
    }

    [Fact]
    public async Task LoadAsync_NonExistentFile_ReturnsFailure()
    {
        // Arrange
        RecipeManager manager = new();
        string filePath = Path.Combine(_testDir, "nonexistent.yaml");

        // Act
        Result<Recipe> result = await manager.LoadAsync(filePath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(filePath);
    }

    [Fact]
    public async Task LoadAsync_EmptyFile_ReturnsFailure()
    {
        // Arrange
        RecipeManager manager = new();
        string filePath = Path.Combine(_testDir, "empty.yaml");
        await File.WriteAllTextAsync(filePath, string.Empty);

        // Act
        Result<Recipe> result = await manager.LoadAsync(filePath);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_InvalidYaml_ReturnsFailure()
    {
        // Arrange
        RecipeManager manager = new();
        string filePath = Path.Combine(_testDir, "invalid.yaml");
        // A line with no ': ' separator produces a "Malformed root-level line" parse error
        await File.WriteAllTextAsync(filePath, "not_valid_yaml_no_colon_separator");

        // Act
        Result<Recipe> result = await manager.LoadAsync(filePath);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_InvalidDirectoryPath_ReturnsFailure()
    {
        // Arrange
        RecipeManager manager = new();
        string filePath = Path.Combine(_testDir, "nonexistent_subdir", "recipe.yaml");
        Recipe recipe = new() { Name = "test", Actions = [] };

        // Act
        Result result = await manager.SaveAsync(recipe, filePath);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_NullRecipe_ThrowsArgumentNullException()
    {
        // Arrange
        RecipeManager manager = new();
        string filePath = Path.Combine(_testDir, "recipe.yaml");

        // Act
        Func<Task<Result>> act = async () => await manager.SaveAsync(null!, filePath);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAsync_NullFilePath_ThrowsArgumentNullException()
    {
        // Arrange
        RecipeManager manager = new();
        Recipe recipe = new() { Name = "test", Actions = [] };

        // Act
        Func<Task<Result>> act = async () => await manager.SaveAsync(recipe, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAsync_WhitespaceFilePath_ThrowsArgumentException()
    {
        // Arrange
        RecipeManager manager = new();
        Recipe recipe = new() { Name = "test", Actions = [] };

        // Act
        Func<Task<Result>> act = async () => await manager.SaveAsync(recipe, "   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LoadAsync_NullFilePath_ThrowsArgumentNullException()
    {
        // Arrange
        RecipeManager manager = new();

        // Act
        Func<Task<Result<Recipe>>> act = async () => await manager.LoadAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task LoadAsync_WhitespaceFilePath_ThrowsArgumentException()
    {
        // Arrange
        RecipeManager manager = new();

        // Act
        Func<Task<Result<Recipe>>> act = async () => await manager.LoadAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        RecipeManager manager = new();
        string filePath = Path.Combine(_testDir, "recipe.yaml");
        Recipe recipe = new() { Name = "test", Actions = [] };
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        Func<Task<Result>> act = async () => await manager.SaveAsync(recipe, filePath, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        RecipeManager manager = new();
        string filePath = Path.Combine(_testDir, "recipe.yaml");
        await File.WriteAllTextAsync(filePath, "name: \"test\"\nactions: []");
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        Func<Task<Result<Recipe>>> act = async () => await manager.LoadAsync(filePath, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

using AwesomeAssertions;
using Refedle.App.Tui.UI;
using Refedle.Engine;

namespace Refedle.Tests.App.Tui.UI;

public sealed class TuiArgumentParserTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsBothNull()
    {
        // Arrange
        string[] args = [];

        // Act
        Result<TuiStartupOptions> result = TuiArgumentParser.Parse(args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InputFile.Should().BeNull();
        result.Value.RecipeFile.Should().BeNull();
    }

    [Fact]
    public void Parse_FileFlag_SetsInputFile()
    {
        // Arrange
        string[] args = ["--file", "path.csv"];

        // Act
        Result<TuiStartupOptions> result = TuiArgumentParser.Parse(args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InputFile.Should().Be("path.csv");
        result.Value.RecipeFile.Should().BeNull();
    }

    [Fact]
    public void Parse_RecipeFlag_SetsRecipeFile()
    {
        // Arrange
        string[] args = ["--recipe", "recipe.yaml"];

        // Act
        Result<TuiStartupOptions> result = TuiArgumentParser.Parse(args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InputFile.Should().BeNull();
        result.Value.RecipeFile.Should().Be("recipe.yaml");
    }

    [Fact]
    public void Parse_BothFlags_SetsBothProperties()
    {
        // Arrange
        string[] args = ["--file", "path.csv", "--recipe", "recipe.yaml"];

        // Act
        Result<TuiStartupOptions> result = TuiArgumentParser.Parse(args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InputFile.Should().Be("path.csv");
        result.Value.RecipeFile.Should().Be("recipe.yaml");
    }

    [Fact]
    public void Parse_FileFlag_WithoutValue_ReturnsFailure()
    {
        // Arrange
        string[] args = ["--file"];

        // Act
        Result<TuiStartupOptions> result = TuiArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("--file");
    }

    [Fact]
    public void Parse_RecipeFlag_WithoutValue_ReturnsFailure()
    {
        // Arrange
        string[] args = ["--recipe"];

        // Act
        Result<TuiStartupOptions> result = TuiArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("--recipe");
    }

    [Fact]
    public void Parse_UnknownFlag_ReturnsFailure()
    {
        // Arrange
        string[] args = ["--unknown"];

        // Act
        Result<TuiStartupOptions> result = TuiArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("--unknown");
    }

    [Fact]
    public void Parse_RecipeBeforeFile_SetsCorrectly()
    {
        // Arrange
        string[] args = ["--recipe", "recipe.yaml", "--file", "path.csv"];

        // Act
        Result<TuiStartupOptions> result = TuiArgumentParser.Parse(args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InputFile.Should().Be("path.csv");
        result.Value.RecipeFile.Should().Be("recipe.yaml");
    }

    [Fact]
    public void Parse_DuplicateFileFlag_ReturnsFailure()
    {
        // Arrange
        string[] args = ["--file", "path1.csv", "--file", "path2.csv"];

        // Act
        Result<TuiStartupOptions> result = TuiArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("--file");
    }

    [Fact]
    public void Parse_DuplicateRecipeFlag_ReturnsFailure()
    {
        // Arrange
        string[] args = ["--recipe", "r1.yaml", "--recipe", "r2.yaml"];

        // Act
        Result<TuiStartupOptions> result = TuiArgumentParser.Parse(args);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("--recipe");
    }

    [Fact]
    public void Parse_FilePathWithSpaces_SetsCorrectPath()
    {
        // Arrange
        string[] args = ["--file", "my file.csv"];

        // Act
        Result<TuiStartupOptions> result = TuiArgumentParser.Parse(args);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.InputFile.Should().Be("my file.csv");
        result.Value.RecipeFile.Should().BeNull();
    }
}

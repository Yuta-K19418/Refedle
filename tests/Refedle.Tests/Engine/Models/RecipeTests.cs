using AwesomeAssertions;
using Refedle.Engine.Models;

namespace Refedle.Tests.Engine.Models;

public sealed class RecipeTests
{
    [Fact]
    public void IsEmpty_WithNoActions_ReturnsTrue()
    {
        // Arrange
        Recipe recipe = new() { Name = "Empty Recipe", Actions = [] };

        // Act
        bool isEmpty = recipe.IsEmpty;

        // Assert
        isEmpty.Should().BeTrue();
    }

    [Fact]
    public void LastModified_WithEquivalentInstants_HasEqualValues()
    {
        // Arrange
        DateTimeOffset utcTime = new(2025, 12, 30, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset jstTime = new(2025, 12, 30, 21, 0, 0, TimeSpan.FromHours(9));
        Recipe utcRecipe = new() { Name = "UTC", Actions = [], LastModified = utcTime };
        Recipe jstRecipe = new() { Name = "JST", Actions = [], LastModified = jstTime };

        // Act
        bool timestampsAreEqual = utcRecipe.LastModified == jstRecipe.LastModified;

        // Assert
        timestampsAreEqual.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullName_ThrowsArgumentException()
    {
        // Arrange & Act
        Func<Recipe> act = () => new Recipe { Name = null!, Actions = [] };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange & Act
        Func<Recipe> act = () => new Recipe { Name = string.Empty, Actions = [] };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhitespaceName_ThrowsArgumentException()
    {
        // Arrange & Act
        Func<Recipe> act = () => new Recipe { Name = "   ", Actions = [] };

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}

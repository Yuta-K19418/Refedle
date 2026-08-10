using AwesomeAssertions;
using Refedle.Engine.Models;

namespace Refedle.Tests.Engine.Models;

public sealed class RecipeTests
{
    [Fact]
    public void IsEmpty_WithNoActions_ReturnsTrue()
    {
        // Arrange
        var recipe = new Recipe { Name = "Empty Recipe", Actions = [] };

        // Act
        var isEmpty = recipe.IsEmpty;

        // Assert
        isEmpty.Should().BeTrue();
    }

    [Fact]
    public void LastModified_WithEquivalentInstants_HasEqualValues()
    {
        // Arrange
        var utcTime = new DateTimeOffset(2025, 12, 30, 12, 0, 0, TimeSpan.Zero);
        var jstTime = new DateTimeOffset(2025, 12, 30, 21, 0, 0, TimeSpan.FromHours(9));
        var utcRecipe = new Recipe { Name = "UTC", Actions = [], LastModified = utcTime };
        var jstRecipe = new Recipe { Name = "JST", Actions = [], LastModified = jstTime };

        // Act
        var timestampsAreEqual = utcRecipe.LastModified == jstRecipe.LastModified;

        // Assert
        timestampsAreEqual.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new Recipe { Name = null!, Actions = [] };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new Recipe { Name = string.Empty, Actions = [] };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhitespaceName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new Recipe { Name = "   ", Actions = [] };

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}

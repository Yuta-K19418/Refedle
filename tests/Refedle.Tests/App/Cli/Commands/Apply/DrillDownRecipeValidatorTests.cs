using AwesomeAssertions;
using Refedle.App.Cli.Commands.Apply;
using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Cli.Commands.Apply;

public sealed class DrillDownRecipeValidatorTests
{
    private const string RecipeName = "Test Recipe";

    private static readonly IReadOnlyList<KeyPathSegment> _testKeyPath =
        [new("orders", KeyPathSegmentKind.Key)];

    private static Recipe CreateRecipe(IReadOnlyList<KeyPathSegment>? drillDownKeyPath) =>
        new() { Name = RecipeName, Actions = [], DrillDownKeyPath = drillDownKeyPath };

    [Fact]
    public void Validate_WithNullRecipe_ThrowsArgumentNullException()
    {
        // Arrange
        Recipe? recipe = null;

        // Act
        Func<Result> act = () => DrillDownRecipeValidator.Validate(DataFormat.JsonObject, recipe!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("recipe");
    }

    [Theory]
    [InlineData(DataFormat.JsonObject)]
    [InlineData(DataFormat.JsonArray)]
    public void Validate_WithDrillDownFormatAndEmptyKeyPath_ReturnsSuccess(DataFormat inputFormat)
    {
        // Arrange
        // A present-but-empty KeyPath is distinct from null: the validator checks
        // presence only and must not reject it based on segment count.
        Recipe recipe = CreateRecipe([]);

        // Act
        Result result = DrillDownRecipeValidator.Validate(inputFormat, recipe);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(DataFormat.JsonObject)]
    [InlineData(DataFormat.JsonArray)]
    public void Validate_WithDrillDownFormatAndNullKeyPath_ReturnsFailure(DataFormat inputFormat)
    {
        // Arrange
        Recipe recipe = CreateRecipe(drillDownKeyPath: null);

        // Act
        Result result = DrillDownRecipeValidator.Validate(inputFormat, recipe);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(RecipeName);
        result.Error.Should().Contain(inputFormat.ToString());
        result.Error.Should().Contain("no DrillDown scope");
    }

    [Theory]
    [InlineData(DataFormat.Csv)]
    [InlineData(DataFormat.JsonLines)]
    public void Validate_WithNonDrillDownFormatAndNullKeyPath_ReturnsSuccess(DataFormat inputFormat)
    {
        // Arrange
        Recipe recipe = CreateRecipe(drillDownKeyPath: null);

        // Act
        Result result = DrillDownRecipeValidator.Validate(inputFormat, recipe);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(DataFormat.Csv)]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    [InlineData(DataFormat.JsonObject)]
    public void Validate_WithKeyPath_ReturnsSuccessForAllFormats(DataFormat inputFormat)
    {
        // Arrange
        Recipe recipe = CreateRecipe(_testKeyPath);

        // Act
        Result result = DrillDownRecipeValidator.Validate(inputFormat, recipe);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}

using AwesomeAssertions;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Recipes;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.Recipes;

public sealed class RecipeYamlSerializerTests
{
    [Fact]
    public void Serialize_EmptyActions_ProducesActionsEmptyListLine()
    {
        // Arrange
        var recipe = new Recipe { Name = "test", Actions = [] };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("actions: []");
    }

    [Fact]
    public void Serialize_WithRenameAction_ProducesCorrectYaml()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "old", NewName = "new" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("  - type: Rename");
        yaml.Should().Contain("    oldName: \"old\"");
        yaml.Should().Contain("    newName: \"new\"");
    }

    [Fact]
    public void Serialize_WithDeleteAction_ProducesCorrectYaml()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new DeleteColumnAction { ColumnName = "temp_field" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("  - type: Delete");
        yaml.Should().Contain("    columnName: \"temp_field\"");
    }

    [Fact]
    public void Serialize_WithCastAction_ProducesCorrectYaml()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new CastColumnAction { ColumnName = "age", TargetType = ColumnType.WholeNumber }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("  - type: Cast");
        yaml.Should().Contain("    columnName: \"age\"");
        yaml.Should().Contain("    targetType: WholeNumber");
    }

    [Fact]
    public void Serialize_WithFilterAction_ProducesCorrectYaml()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [FilterAction.Create("status", FilterOperator.Equals, ComparisonType.Text, "active").Value],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("  - type: Filter");
        yaml.Should().Contain("    columnName: \"status\"");
        yaml.Should().Contain("    operator: Equals");
        yaml.Should().Contain("    comparisonType: Text");
        yaml.Should().Contain("    value: \"active\"");
    }

    [Fact]
    public void Serialize_NullDescription_OmitsDescriptionField()
    {
        // Arrange
        var recipe = new Recipe { Name = "test", Actions = [], Description = null };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().NotContain("description:");
    }

    [Fact]
    public void Serialize_NullLastModified_OmitsLastModifiedField()
    {
        // Arrange
        var recipe = new Recipe { Name = "test", Actions = [], LastModified = null };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().NotContain("lastModified:");
    }

    [Fact]
    public void Serialize_WithLastModified_WritesIsoTimestampUnquoted()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            LastModified = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Actions = [],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("lastModified: 2025-01-01T00:00:00.0000000+00:00");
    }

    [Fact]
    public void Serialize_StringValueWithDoubleQuote_EscapesQuoteCharacter()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "col\"name", NewName = "new" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("    oldName: \"col\\\"name\"");
    }

    [Fact]
    public void Serialize_StringValueWithBackslash_EscapesBackslash()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = @"C:\data", NewName = "output" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain(@"    oldName: ""C:\\data""");
    }

    [Fact]
    public void Serialize_StringValueWithBackslashAndQuote_EscapesBoth()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "col\\\"name", NewName = "output" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("    oldName: \"col\\\\\\\"name\"");
    }

    [Fact]
    public void Serialize_WithFillAction_RoundTrip_ProducesCorrectYaml()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new FillColumnAction { ColumnName = "Email", Value = "REDACTED" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("  - type: Fill");
        yaml.Should().Contain("    columnName: \"Email\"");
        yaml.Should().Contain("    value: \"REDACTED\"");
    }

    [Fact]
    public void Serialize_WithFillAction_ValueWithSpecialChars_EscapesCorrectly()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new FillColumnAction { ColumnName = "col", Value = "val\"with\\special" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("    value: \"val\\\"with\\\\special\"");
    }

    [Fact]
    public void Serialize_FieldOrder_NameFirstActionsLast()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Description = "desc",
            LastModified = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Actions = [],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        var nameIdx = yaml.IndexOf("name:", StringComparison.Ordinal);
        var descIdx = yaml.IndexOf("description:", StringComparison.Ordinal);
        var lastModIdx = yaml.IndexOf("lastModified:", StringComparison.Ordinal);
        var actionsIdx = yaml.IndexOf("actions:", StringComparison.Ordinal);
        nameIdx.Should().BeGreaterThanOrEqualTo(0, "name field should be present");
        descIdx.Should().BeGreaterThanOrEqualTo(0, "description field should be present");
        lastModIdx.Should().BeGreaterThanOrEqualTo(0, "lastModified field should be present");
        actionsIdx.Should().BeGreaterThanOrEqualTo(0, "actions field should be present");
        nameIdx.Should().BeLessThan(descIdx);
        descIdx.Should().BeLessThan(lastModIdx);
        lastModIdx.Should().BeLessThan(actionsIdx);
    }

    [Fact]
    public void Serialize_WithFormatTimestampAction_ProducesCorrectYaml()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new FormatTimestampAction { ColumnName = "CreatedAt", TargetFormat = "yyyy/MM/dd" }],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("  - type: FormatTimestamp");
        yaml.Should().Contain("    columnName: \"CreatedAt\"");
        yaml.Should().Contain("    targetFormat: \"yyyy/MM/dd\"");
    }

    [Fact]
    public void Serialize_NullDrillDownKeyPath_OmitsSection()
    {
        // Arrange
        var recipe = new Recipe { Name = "test", Actions = [], DrillDownKeyPath = null };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().NotContain("drillDownKeyPath:");
    }

    [Fact]
    public void Serialize_EmptyDrillDownKeyPath_ProducesDrillDownKeyPathEmptyListLine()
    {
        // Arrange
        var recipe = new Recipe { Name = "test", Actions = [], DrillDownKeyPath = [] };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("drillDownKeyPath: []");
    }

    [Fact]
    public void Serialize_WithDrillDownKeyPath_KeyThenIndexSegments_CombinesIntoOneItem()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [],
            DrillDownKeyPath =
            [
                new KeyPathSegment("customer", KeyPathSegmentKind.Key),
                new KeyPathSegment("orders", KeyPathSegmentKind.Key),
                new KeyPathSegment("[0]", KeyPathSegmentKind.Index),
            ],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("drillDownKeyPath:\n  - key: \"customer\"\n  - key: \"orders\"\n    index: 0\n");
    }

    [Fact]
    public void Serialize_WithDrillDownKeyPath_BareIndexNotPrecededByKey_ProducesStandaloneIndexItem()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [],
            DrillDownKeyPath =
            [
                new KeyPathSegment("scores", KeyPathSegmentKind.Key),
                new KeyPathSegment("[1]", KeyPathSegmentKind.Index),
                new KeyPathSegment("[0]", KeyPathSegmentKind.Index),
            ],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Contain("drillDownKeyPath:\n  - key: \"scores\"\n    index: 1\n  - index: 0\n");
    }

    [Fact]
    public void Serialize_WithDrillDownKeyPath_AppearsAfterActions()
    {
        // Arrange
        var recipe = new Recipe
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "a", NewName = "b" }],
            DrillDownKeyPath = [new KeyPathSegment("customer", KeyPathSegmentKind.Key)],
        };

        // Act
        var yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        var actionsIdx = yaml.IndexOf("actions:", StringComparison.Ordinal);
        var drillDownIdx = yaml.IndexOf("drillDownKeyPath:", StringComparison.Ordinal);
        actionsIdx.Should().BeLessThan(drillDownIdx);
    }
}

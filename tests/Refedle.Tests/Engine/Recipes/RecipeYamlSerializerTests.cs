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
        Recipe recipe = new() { Name = "test", Actions = [] };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions: []\n");
    }

    [Fact]
    public void Serialize_WithRenameAction_ProducesCorrectYaml()
    {
        // Arrange
        Recipe recipe = new()
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "old", NewName = "new" }],
        };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions:\n  - type: Rename\n    oldName: \"old\"\n    newName: \"new\"\n");
    }

    [Fact]
    public void Serialize_WithDeleteAction_ProducesCorrectYaml()
    {
        // Arrange
        Recipe recipe = new()
        {
            Name = "test",
            Actions = [new DeleteColumnAction { ColumnName = "temp_field" }],
        };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions:\n  - type: Delete\n    columnName: \"temp_field\"\n");
    }

    [Fact]
    public void Serialize_WithCastAction_ProducesCorrectYaml()
    {
        // Arrange
        Recipe recipe = new()
        {
            Name = "test",
            Actions = [new CastColumnAction { ColumnName = "age", TargetType = ColumnType.WholeNumber }],
        };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions:\n  - type: Cast\n    columnName: \"age\"\n    targetType: WholeNumber\n");
    }

    [Fact]
    public void Serialize_WithFilterAction_ProducesCorrectYaml()
    {
        // Arrange
        Recipe recipe = new()
        {
            Name = "test",
            Actions = [FilterAction.Create("status", FilterOperator.Equals, ComparisonType.Text, "active").Value],
        };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions:\n  - type: Filter\n    columnName: \"status\"\n    operator: Equals\n    comparisonType: Text\n    value: \"active\"\n");
    }

    [Fact]
    public void Serialize_NullDescription_OmitsDescriptionField()
    {
        // Arrange
        Recipe recipe = new() { Name = "test", Actions = [], Description = null };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().NotContain("description:");
    }

    [Fact]
    public void Serialize_NullLastModified_OmitsLastModifiedField()
    {
        // Arrange
        Recipe recipe = new() { Name = "test", Actions = [], LastModified = null };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().NotContain("lastModified:");
    }

    [Fact]
    public void Serialize_WithLastModified_WritesIsoTimestampUnquoted()
    {
        // Arrange
        Recipe recipe = new()
        {
            Name = "test",
            LastModified = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Actions = [],
        };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nlastModified: 2025-01-01T00:00:00.0000000+00:00\nactions: []\n");
    }

    [Fact]
    public void Serialize_StringValueWithDoubleQuote_EscapesQuoteCharacter()
    {
        // Arrange
        Recipe recipe = new()
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "col\"name", NewName = "new" }],
        };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions:\n  - type: Rename\n    oldName: \"col\\\"name\"\n    newName: \"new\"\n");
    }

    [Fact]
    public void Serialize_StringValueWithBackslash_EscapesBackslash()
    {
        // Arrange
        Recipe recipe = new()
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = @"C:\data", NewName = "output" }],
        };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions:\n  - type: Rename\n    oldName: \"C:\\\\data\"\n    newName: \"output\"\n");
    }

    [Fact]
    public void Serialize_StringValueWithBackslashAndQuote_EscapesBoth()
    {
        // Arrange
        Recipe recipe = new()
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "col\\\"name", NewName = "output" }],
        };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions:\n  - type: Rename\n    oldName: \"col\\\\\\\"name\"\n    newName: \"output\"\n");
    }

    [Fact]
    public void Serialize_WithFillAction_RoundTrip_ProducesCorrectYaml()
    {
        // Arrange
        Recipe recipe = new()
        {
            Name = "test",
            Actions = [new FillColumnAction { ColumnName = "Email", Value = "REDACTED" }],
        };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions:\n  - type: Fill\n    columnName: \"Email\"\n    value: \"REDACTED\"\n");
    }

    [Fact]
    public void Serialize_WithFillAction_ValueWithSpecialChars_EscapesCorrectly()
    {
        // Arrange
        Recipe recipe = new()
        {
            Name = "test",
            Actions = [new FillColumnAction { ColumnName = "col", Value = "val\"with\\special" }],
        };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions:\n  - type: Fill\n    columnName: \"col\"\n    value: \"val\\\"with\\\\special\"\n");
    }

    [Fact]
    public void Serialize_FieldOrder_NameFirstActionsLast()
    {
        // Arrange
        Recipe recipe = new()
        {
            Name = "test",
            Description = "desc",
            LastModified = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Actions = [],
        };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\ndescription: \"desc\"\nlastModified: 2025-01-01T00:00:00.0000000+00:00\nactions: []\n");
    }

    [Fact]
    public void Serialize_WithFormatTimestampAction_ProducesCorrectYaml()
    {
        // Arrange
        Recipe recipe = new()
        {
            Name = "test",
            Actions = [new FormatTimestampAction { ColumnName = "CreatedAt", TargetFormat = "yyyy/MM/dd" }],
        };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions:\n  - type: FormatTimestamp\n    columnName: \"CreatedAt\"\n    targetFormat: \"yyyy/MM/dd\"\n");
    }

    [Fact]
    public void Serialize_NullDrillDownKeyPath_OmitsSection()
    {
        // Arrange
        Recipe recipe = new() { Name = "test", Actions = [], DrillDownKeyPath = null };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().NotContain("drillDownKeyPath:");
    }

    [Fact]
    public void Serialize_EmptyDrillDownKeyPath_ProducesDrillDownKeyPathEmptyListLine()
    {
        // Arrange
        Recipe recipe = new() { Name = "test", Actions = [], DrillDownKeyPath = [] };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions: []\ndrillDownKeyPath: []\n");
    }

    [Fact]
    public void Serialize_WithDrillDownKeyPath_KeyThenIndexSegments_CombinesIntoOneItem()
    {
        // Arrange
        Recipe recipe = new()
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
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions: []\ndrillDownKeyPath:\n  - key: \"customer\"\n  - key: \"orders\"\n    index: 0\n");
    }

    [Fact]
    public void Serialize_WithDrillDownKeyPath_BareIndexNotPrecededByKey_ProducesStandaloneIndexItem()
    {
        // Arrange
        Recipe recipe = new()
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
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        yaml.Should().Be("name: \"test\"\nactions: []\ndrillDownKeyPath:\n  - key: \"scores\"\n    index: 1\n  - index: 0\n");
    }

    [Fact]
    public void Serialize_WithDrillDownKeyPath_AppearsAfterActions()
    {
        // Arrange
        Recipe recipe = new()
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "a", NewName = "b" }],
            DrillDownKeyPath = [new KeyPathSegment("customer", KeyPathSegmentKind.Key)],
        };

        // Act
        string yaml = RecipeYamlSerializer.Serialize(recipe);

        // Assert
        int actionsIdx = yaml.IndexOf("actions:", StringComparison.Ordinal);
        int drillDownIdx = yaml.IndexOf("drillDownKeyPath:", StringComparison.Ordinal);
        actionsIdx.Should().BeLessThan(drillDownIdx);
    }
}

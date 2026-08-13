using AwesomeAssertions;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Recipes;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.Recipes;

public sealed class RecipeYamlParserTests
{
    // -----------------------------------------------------------------------
    // Parse
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_ValidYaml_ReturnsRecipeWithCorrectName()
    {
        // Arrange
        var yaml = "name: \"customer-data\"\nactions: []";

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("customer-data");
    }

    [Fact]
    public void Parse_EmptyActionList_ReturnsEmptyActions()
    {
        // Arrange
        var yaml = "name: \"test\"\nactions: []";

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Parse_RenameAction_ParsesOldNameAndNewName()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: rename
                oldName: "old_col"
                newName: "new_col"
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Actions[0].Should().BeOfType<RenameColumnAction>().Subject;
        action.OldName.Should().Be("old_col");
        action.NewName.Should().Be("new_col");
    }

    [Fact]
    public void Parse_DeleteAction_ParsesColumnName()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: delete
                columnName: "temp_field"
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Actions[0].Should().BeOfType<DeleteColumnAction>().Subject;
        action.ColumnName.Should().Be("temp_field");
    }

    [Fact]
    public void Parse_CastAction_ParsesColumnNameAndTargetType()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: cast
                columnName: "age"
                targetType: WholeNumber
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Actions[0].Should().BeOfType<CastColumnAction>().Subject;
        action.ColumnName.Should().Be("age");
        action.TargetType.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void Parse_FilterAction_ParsesColumnNameOperatorAndValue()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: filter
                columnName: "status"
                operator: Equals
                comparisonType: Text
                value: "active"
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Actions[0].Should().BeOfType<FilterAction>().Subject;
        action.ColumnName.Should().Be("status");
        action.Operator.Should().Be(FilterOperator.Equals);
        action.Value.Should().Be("active");
    }

    [Fact]
    public void Parse_MultipleActions_PreservesOrder()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: rename
                oldName: "a"
                newName: "b"
              - type: delete
                columnName: "temp"
              - type: cast
                columnName: "age"
                targetType: WholeNumber
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Actions.Should().HaveCount(3);
        result.Value.Actions[0].Should().BeOfType<RenameColumnAction>();
        result.Value.Actions[1].Should().BeOfType<DeleteColumnAction>();
        result.Value.Actions[2].Should().BeOfType<CastColumnAction>();
    }

    [Fact]
    public void Parse_UnknownActionType_ReturnsFailure()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: unsupported
                columnName: "col"
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("unsupported");
    }

    [Fact]
    public void Parse_RenameAction_MissingOldName_ReturnsFailure()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: rename
                newName: "new"
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("oldName");
    }

    [Fact]
    public void Parse_RenameAction_MissingNewName_ReturnsFailure()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: rename
                oldName: "old"
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("newName");
    }

    [Fact]
    public void Parse_DeleteAction_MissingColumnName_ReturnsFailure()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: delete
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("columnName");
    }

    [Fact]
    public void Parse_CastAction_MissingColumnName_ReturnsFailure()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: cast
                targetType: WholeNumber
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("columnName");
    }

    [Fact]
    public void Parse_CastAction_MissingTargetType_ReturnsFailure()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: cast
                columnName: "age"
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("targetType");
    }

    [Fact]
    public void Parse_FilterAction_MissingColumnName_ReturnsFailure()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: filter
                operator: Equals
                value: "active"
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("columnName");
    }

    [Fact]
    public void Parse_FilterAction_MissingOperator_ReturnsFailure()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: filter
                columnName: "status"
                value: "active"
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("operator");
    }

    [Fact]
    public void Parse_FilterAction_MissingValue_ReturnsFailure()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: filter
                columnName: "status"
                operator: Equals
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("value");
    }

    [Fact]
    public void Parse_InvalidEnumValue_ReturnsFailure()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: cast
                columnName: "age"
                targetType: InvalidType
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("InvalidType");
    }

    [Fact]
    public void Parse_FilterAction_InvalidOperator_ReturnsFailure()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: filter
                columnName: "status"
                operator: InvalidOperator
                value: "active"
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("InvalidOperator");
    }

    [Fact]
    public void Parse_MissingNameField_ReturnsFailure()
    {
        // Arrange
        var yaml = "actions: []";

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("name");
    }

    [Fact]
    public void Parse_CommentLines_AreIgnored()
    {
        // Arrange
        var yaml = """
            # This is a comment
            name: "test"
            # Another comment
            actions: []
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("test");
    }

    [Fact]
    public void Parse_BlankLines_AreIgnored()
    {
        // Arrange
        var yaml = "\nname: \"test\"\n\nactions: []\n\n";

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("test");
    }

    [Fact]
    public void Parse_EscapedQuoteInStringValue_ParsesCorrectly()
    {
        // Arrange
        var yaml = """
            name: "test"
            actions:
              - type: rename
                oldName: "col\"name"
                newName: "new"
            """;

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Actions[0].Should().BeOfType<RenameColumnAction>().Subject;
        action.OldName.Should().Be("col\"name");
    }

    [Fact]
    public void Parse_UnquotedStringValue_ParsesCorrectly()
    {
        // Arrange
        var yaml = "name: customer-data\nactions: []";

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("customer-data");
    }

    [Fact]
    public void Parse_InvalidLastModified_ReturnsFailure()
    {
        // Arrange
        var yaml = "name: \"test\"\nlastModified: \"not-a-date\"\nactions: []";

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("lastModified");
    }

    [Fact]
    public void Parse_CrlfLineEndings_ParsesCorrectly()
    {
        // Arrange
        var yaml = "name: \"test\"\r\nactions: []\r\n";

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("test");
    }

    [Fact]
    public void Parse_MalformedRootLevelLine_ReturnsFailure()
    {
        // Arrange
        var yaml = "name: \"test\"\njustakeynovalue\nactions: []";

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("justakeynovalue");
    }

    [Fact]
    public void Parse_DuplicateNameKey_ReturnsFailure()
    {
        // Arrange
        var yaml = "name: \"first\"\nname: \"second\"\nactions: []";

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("name");
    }

    [Fact]
    public void Parse_DuplicateDescriptionKey_ReturnsFailure()
    {
        // Arrange
        var yaml = "name: \"test\"\ndescription: \"first\"\ndescription: \"second\"\nactions: []";

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("description");
    }

    [Fact]
    public void Parse_DuplicateLastModifiedKey_ReturnsFailure()
    {
        // Arrange
        var yaml = "name: \"test\"\nlastModified: \"2025-01-01T00:00:00+00:00\"\nlastModified: \"2025-06-01T00:00:00+00:00\"\nactions: []";

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("lastModified");
    }

    [Fact]
    public void Parse_ActionsKeyWithNoItems_ReturnsEmptyActions()
    {
        // Arrange
        var yaml = "name: \"test\"\nactions:";

        // Act
        var result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Actions.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public void RoundTrip_RenameAction_ProducesEquivalentRecipe()
    {
        // Arrange
        var original = new Recipe
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "col_a", NewName = "col_b" }],
        };

        // Act
        var result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("test");
        var action = result.Value.Actions[0].Should().BeOfType<RenameColumnAction>().Subject;
        action.OldName.Should().Be("col_a");
        action.NewName.Should().Be("col_b");
    }

    [Fact]
    public void RoundTrip_DeleteAction_ProducesEquivalentRecipe()
    {
        // Arrange
        var original = new Recipe
        {
            Name = "test",
            Actions = [new DeleteColumnAction { ColumnName = "temp" }],
        };

        // Act
        var result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Actions[0].Should().BeOfType<DeleteColumnAction>().Subject;
        action.ColumnName.Should().Be("temp");
    }

    [Fact]
    public void RoundTrip_CastAction_ProducesEquivalentRecipe()
    {
        // Arrange
        var original = new Recipe
        {
            Name = "test",
            Actions = [new CastColumnAction { ColumnName = "age", TargetType = ColumnType.WholeNumber }],
        };

        // Act
        var result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Actions[0].Should().BeOfType<CastColumnAction>().Subject;
        action.ColumnName.Should().Be("age");
        action.TargetType.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void RoundTrip_FilterAction_ProducesEquivalentRecipe()
    {
        // Arrange
        var original = new Recipe
        {
            Name = "test",
            Actions = [FilterAction.Create("status", FilterOperator.Equals, ComparisonType.Text, "active").Value],
        };

        // Act
        var result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Actions[0].Should().BeOfType<FilterAction>().Subject;
        action.ColumnName.Should().Be("status");
        action.Operator.Should().Be(FilterOperator.Equals);
        action.Value.Should().Be("active");
    }

    [Fact]
    public void RoundTrip_MultipleActions_PreservesAllActions()
    {
        // Arrange
        var original = new Recipe
        {
            Name = "pipeline",
            Actions =
            [
                new RenameColumnAction { OldName = "user_id", NewName = "userId" },
                new DeleteColumnAction { ColumnName = "temp" },
                new CastColumnAction { ColumnName = "age", TargetType = ColumnType.WholeNumber },
                FilterAction.Create("status", FilterOperator.Equals, ComparisonType.Text, "active").Value,
            ],
        };

        // Act
        var result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Actions.Should().HaveCount(4);
        result.Value.Actions[0].Should().BeOfType<RenameColumnAction>();
        result.Value.Actions[1].Should().BeOfType<DeleteColumnAction>();
        result.Value.Actions[2].Should().BeOfType<CastColumnAction>();
        result.Value.Actions[3].Should().BeOfType<FilterAction>();
    }

    [Fact]
    public void RoundTrip_WithNullableFieldsPopulated_PreservesValues()
    {
        // Arrange
        var original = new Recipe
        {
            Name = "full",
            Description = "A description",
            LastModified = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero),
            Actions = [],
        };

        // Act
        var result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("A description");
        result.Value.LastModified.Should().Be(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void RoundTrip_WithNullableFieldsAbsent_ReturnsNulls()
    {
        // Arrange
        var original = new Recipe { Name = "minimal", Actions = [] };

        // Act
        var result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().BeNull();
        result.Value.LastModified.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_BackslashInStringValue_PreservesValue()
    {
        // Arrange
        var original = new Recipe
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = @"C:\data\file", NewName = "output" }],
        };

        // Act
        var result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Actions[0].Should().BeOfType<RenameColumnAction>().Subject;
        action.OldName.Should().Be(@"C:\data\file");
    }

    [Fact]
    public void RoundTrip_BackslashFollowedByQuote_PreservesValue()
    {
        // Arrange
        var original = new Recipe
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "col\\\"name", NewName = "output" }],
        };

        // Act
        var result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Actions[0].Should().BeOfType<RenameColumnAction>().Subject;
        action.OldName.Should().Be("col\\\"name");
    }

    [Theory]
    [InlineData(FilterOperator.Equals, ComparisonType.Text, "v")]
    [InlineData(FilterOperator.NotEquals, ComparisonType.Text, "v")]
    [InlineData(FilterOperator.GreaterThan, ComparisonType.Number, "1")]
    [InlineData(FilterOperator.LessThan, ComparisonType.Number, "1")]
    [InlineData(FilterOperator.GreaterThanOrEqual, ComparisonType.Number, "1")]
    [InlineData(FilterOperator.LessThanOrEqual, ComparisonType.Number, "1")]
    [InlineData(FilterOperator.Contains, ComparisonType.Text, "v")]
    [InlineData(FilterOperator.NotContains, ComparisonType.Text, "v")]
    [InlineData(FilterOperator.StartsWith, ComparisonType.Text, "v")]
    [InlineData(FilterOperator.EndsWith, ComparisonType.Text, "v")]
    public void RoundTrip_FilterAction_AllOperators_PreservesOperator(
        FilterOperator op, ComparisonType comparisonType, string value)
    {
        // Arrange
        var original = new Recipe
        {
            Name = "test",
            Actions = [FilterAction.Create("col", op, comparisonType, value).Value],
        };

        // Act
        var result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Actions[0].Should().BeOfType<FilterAction>().Subject;
        action.Operator.Should().Be(op);
        action.ComparisonType.Should().Be(comparisonType);
    }

    [Theory]
    [InlineData(ColumnType.Text)]
    [InlineData(ColumnType.WholeNumber)]
    [InlineData(ColumnType.FloatingPoint)]
    [InlineData(ColumnType.Boolean)]
    [InlineData(ColumnType.Timestamp)]
    [InlineData(ColumnType.JsonObject)]
    [InlineData(ColumnType.JsonArray)]
    public void RoundTrip_CastAction_AllColumnTypes_PreservesTargetType(ColumnType columnType)
    {
        // Arrange
        var original = new Recipe
        {
            Name = "test",
            Actions = [new CastColumnAction { ColumnName = "col", TargetType = columnType }],
        };

        // Act
        var result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value.Actions[0].Should().BeOfType<CastColumnAction>().Subject;
        action.TargetType.Should().Be(columnType);
    }
}

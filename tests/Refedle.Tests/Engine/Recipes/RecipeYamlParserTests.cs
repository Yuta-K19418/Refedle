using AwesomeAssertions;
using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
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
        string yaml = "name: \"customer-data\"\nactions: []";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("customer-data");
    }

    [Fact]
    public void Parse_EmptyActionList_ReturnsEmptyActions()
    {
        // Arrange
        string yaml = "name: \"test\"\nactions: []";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Parse_RenameAction_ParsesOldNameAndNewName()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Rename
                oldName: "old_col"
                newName: "new_col"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        RenameColumnAction action = result.Value.Actions[0].Should().BeOfType<RenameColumnAction>().Subject;
        action.OldName.Should().Be("old_col");
        action.NewName.Should().Be("new_col");
    }

    [Fact]
    public void Parse_DeleteAction_ParsesColumnName()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Delete
                columnName: "temp_field"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DeleteColumnAction action = result.Value.Actions[0].Should().BeOfType<DeleteColumnAction>().Subject;
        action.ColumnName.Should().Be("temp_field");
    }

    [Fact]
    public void Parse_CastAction_ParsesColumnNameAndTargetType()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Cast
                columnName: "age"
                targetType: WholeNumber
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        CastColumnAction action = result.Value.Actions[0].Should().BeOfType<CastColumnAction>().Subject;
        action.ColumnName.Should().Be("age");
        action.TargetType.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void Parse_FilterAction_ParsesColumnNameOperatorAndValue()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Filter
                columnName: "status"
                operator: Equals
                comparisonType: Text
                value: "active"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        FilterAction action = result.Value.Actions[0].Should().BeOfType<FilterAction>().Subject;
        action.ColumnName.Should().Be("status");
        action.Operator.Should().Be(FilterOperator.Equals);
        action.Value.Should().Be("active");
    }

    [Fact]
    public void Parse_MultipleActions_PreservesOrder()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Rename
                oldName: "a"
                newName: "b"
              - type: Delete
                columnName: "temp"
              - type: Cast
                columnName: "age"
                targetType: WholeNumber
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

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
        string yaml = """
            name: "test"
            actions:
              - type: unsupported
                columnName: "col"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("unsupported");
    }

    [Fact]
    public void Parse_RenameAction_MissingOldName_ReturnsFailure()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Rename
                newName: "new"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("oldName");
    }

    [Fact]
    public void Parse_RenameAction_MissingNewName_ReturnsFailure()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Rename
                oldName: "old"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("newName");
    }

    [Fact]
    public void Parse_DeleteAction_MissingColumnName_ReturnsFailure()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Delete
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("columnName");
    }

    [Fact]
    public void Parse_CastAction_MissingColumnName_ReturnsFailure()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Cast
                targetType: WholeNumber
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("columnName");
    }

    [Fact]
    public void Parse_CastAction_MissingTargetType_ReturnsFailure()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Cast
                columnName: "age"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("targetType");
    }

    [Fact]
    public void Parse_FilterAction_MissingColumnName_ReturnsFailure()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Filter
                operator: Equals
                value: "active"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("columnName");
    }

    [Fact]
    public void Parse_FilterAction_MissingOperator_ReturnsFailure()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Filter
                columnName: "status"
                value: "active"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("operator");
    }

    [Fact]
    public void Parse_FilterAction_MissingValue_ReturnsFailure()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Filter
                columnName: "status"
                operator: Equals
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("value");
    }

    [Fact]
    public void Parse_InvalidEnumValue_ReturnsFailure()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Cast
                columnName: "age"
                targetType: InvalidType
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("InvalidType");
    }

    [Fact]
    public void Parse_FilterAction_InvalidOperator_ReturnsFailure()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Filter
                columnName: "status"
                operator: InvalidOperator
                value: "active"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("InvalidOperator");
    }

    [Fact]
    public void Parse_MissingNameField_ReturnsFailure()
    {
        // Arrange
        string yaml = "actions: []";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("name");
    }

    [Fact]
    public void Parse_CommentLines_AreIgnored()
    {
        // Arrange
        string yaml = """
            # This is a comment
            name: "test"
            # Another comment
            actions: []
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("test");
    }

    [Fact]
    public void Parse_BlankLines_AreIgnored()
    {
        // Arrange
        string yaml = "\nname: \"test\"\n\nactions: []\n\n";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("test");
    }

    [Fact]
    public void Parse_EscapedQuoteInStringValue_ParsesCorrectly()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Rename
                oldName: "col\"name"
                newName: "new"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        RenameColumnAction action = result.Value.Actions[0].Should().BeOfType<RenameColumnAction>().Subject;
        action.OldName.Should().Be("col\"name");
    }

    [Fact]
    public void Parse_UnquotedStringValue_ParsesCorrectly()
    {
        // Arrange
        string yaml = "name: customer-data\nactions: []";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("customer-data");
    }

    [Fact]
    public void Parse_InvalidLastModified_ReturnsFailure()
    {
        // Arrange
        string yaml = "name: \"test\"\nlastModified: \"not-a-date\"\nactions: []";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("lastModified");
    }

    [Fact]
    public void Parse_CrlfLineEndings_ParsesCorrectly()
    {
        // Arrange
        string yaml = "name: \"test\"\r\nactions: []\r\n";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("test");
    }

    [Fact]
    public void Parse_MalformedRootLevelLine_ReturnsFailure()
    {
        // Arrange
        string yaml = "name: \"test\"\njustakeynovalue\nactions: []";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("justakeynovalue");
    }

    [Fact]
    public void Parse_DuplicateNameKey_ReturnsFailure()
    {
        // Arrange
        string yaml = "name: \"first\"\nname: \"second\"\nactions: []";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("name");
    }

    [Fact]
    public void Parse_DuplicateDescriptionKey_ReturnsFailure()
    {
        // Arrange
        string yaml = "name: \"test\"\ndescription: \"first\"\ndescription: \"second\"\nactions: []";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("description");
    }

    [Fact]
    public void Parse_DuplicateLastModifiedKey_ReturnsFailure()
    {
        // Arrange
        string yaml = "name: \"test\"\nlastModified: \"2025-01-01T00:00:00+00:00\"\nlastModified: \"2025-06-01T00:00:00+00:00\"\nactions: []";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("lastModified");
    }

    [Fact]
    public void Parse_ActionsKeyWithNoItems_ReturnsEmptyActions()
    {
        // Arrange
        string yaml = "name: \"test\"\nactions:";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MalformedActionField_ReturnsFailureWithOriginalIndentedLine()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Rename
                badfieldnocolon
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Malformed action field: '    badfieldnocolon'");
    }

    [Fact]
    public void Parse_InvalidPendingActionAtNextActionBoundary_ReturnsFailure()
    {
        // Arrange: Rename is missing "newName" — finalized when the next action item begins.
        string yaml = """
            name: "test"
            actions:
              - type: Rename
                oldName: "a"
              - type: Delete
                columnName: "x"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("newName");
    }

    [Theory]
    [InlineData("name: \"test\"\nactions:\nbadline")]
    [InlineData("name: \"test\"\nactions:\n  - type: Delete\nbadline")]
    public void Parse_UnexpectedLineInActionsContext_ReturnsFailure(string yaml)
    {
        // Arrange: neither an action-item boundary ("  - type: ") nor a nested field ("    ")
        // line, exercised both before and after an action item has started.

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Unexpected line in actions context: 'badline'");
    }

    [Theory]
    [InlineData("drillDownKeyPath:")]
    [InlineData("drillDownKeyPath: []")]
    public void Parse_InvalidPendingActionAtDrillDownKeyPathTransition_ReturnsFailure(string drillDownKeyPathLine)
    {
        // Arrange: Rename is missing "newName" — finalized when the actions section transitions
        // into drillDownKeyPath parsing, via either transition line form.
        string yaml = $"name: \"test\"\nactions:\n  - type: Rename\n    oldName: \"a\"\n{drillDownKeyPathLine}";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("newName");
    }

    // -----------------------------------------------------------------------
    // DrillDownKeyPath
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_DrillDownKeyPath_KeyOnlyItem_ParsesAsKeySegment()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions: []
            drillDownKeyPath:
              - key: "customer"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DrillDownKeyPath.Should().Equal(new KeyPathSegment("customer", KeyPathSegmentKind.Key));
    }

    [Fact]
    public void Parse_DrillDownKeyPath_IndexOnlyItem_ParsesAsIndexSegment()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions: []
            drillDownKeyPath:
              - index: 0
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DrillDownKeyPath.Should().Equal(new KeyPathSegment("[0]", KeyPathSegmentKind.Index));
    }

    [Fact]
    public void Parse_DrillDownKeyPath_KeyThenIndexItem_ParsesAsKeySegmentFollowedByIndexSegment()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions: []
            drillDownKeyPath:
              - key: "orders"
                index: 0
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DrillDownKeyPath.Should().Equal(
            new KeyPathSegment("orders", KeyPathSegmentKind.Key),
            new KeyPathSegment("[0]", KeyPathSegmentKind.Index));
    }

    [Fact]
    public void Parse_DrillDownKeyPath_MultipleItems_PreservesOrder()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions: []
            drillDownKeyPath:
              - key: "customer"
              - key: "orders"
                index: 0
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DrillDownKeyPath.Should().Equal(
            new KeyPathSegment("customer", KeyPathSegmentKind.Key),
            new KeyPathSegment("orders", KeyPathSegmentKind.Key),
            new KeyPathSegment("[0]", KeyPathSegmentKind.Index));
    }

    [Fact]
    public void Parse_DrillDownKeyPath_ItemMissingKeyAndIndex_ReturnsFailure()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions: []
            drillDownKeyPath:
              - notAField: "value"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("key").And.Contain("index");
    }

    [Fact]
    public void Parse_DrillDownKeyPath_InvalidPendingItemAtNextItemBoundary_ReturnsFailure()
    {
        // Arrange: the first item is missing both "key" and "index" — finalized when the
        // next item boundary ("  - ") begins, not at end-of-file.
        string yaml = """
            name: "test"
            actions: []
            drillDownKeyPath:
              - notAField: "value"
              - key: "customer"
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("key").And.Contain("index");
    }

    [Fact]
    public void Parse_DrillDownKeyPath_MalformedIndexValue_ReturnsFailure()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions: []
            drillDownKeyPath:
              - key: "orders"
                index: notANumber
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("notANumber");
    }

    [Fact]
    public void Parse_MalformedDrillDownKeyPathField_ReturnsFailureWithOriginalIndentedLine()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions: []
            drillDownKeyPath:
              - key: "customer"
                badfieldnocolon
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Malformed action field: '    badfieldnocolon'");
    }

    [Fact]
    public void Parse_MalformedDrillDownKeyPathItemBoundaryLine_ReturnsFailure()
    {
        // Arrange: the item-boundary line itself ("  - ...") has no ": " separator.
        string yaml = "name: \"test\"\nactions: []\ndrillDownKeyPath:\n  - badnocolon";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Malformed action field: '  - badnocolon'");
    }

    [Fact]
    public void Parse_UnexpectedLineInDrillDownKeyPathContext_ReturnsFailure()
    {
        // Arrange: neither an item boundary ("  - ") nor a nested field ("    ") line.
        string yaml = "name: \"test\"\nactions: []\ndrillDownKeyPath:\n  - key: \"customer\"\nbadline";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Unexpected line in drillDownKeyPath context: 'badline'");
    }

    [Fact]
    public void Parse_MissingDrillDownKeyPath_ReturnsNull()
    {
        // Arrange
        string yaml = "name: \"test\"\nactions: []";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DrillDownKeyPath.Should().BeNull();
    }

    [Fact]
    public void Parse_DrillDownKeyPathEmptyList_ReturnsEmptyDrillDownKeyPathNotNull()
    {
        // Arrange
        string yaml = "name: \"test\"\nactions: []\ndrillDownKeyPath: []";

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DrillDownKeyPath.Should().NotBeNull();
        result.Value.DrillDownKeyPath.Should().BeEmpty();
    }

    [Fact]
    public void Parse_DrillDownKeyPathEmptyList_AfterNonEmptyActions_ReturnsEmptyDrillDownKeyPathNotNull()
    {
        // Arrange
        string yaml = """
            name: "test"
            actions:
              - type: Rename
                oldName: "a"
                newName: "b"
            drillDownKeyPath: []
            """;

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(yaml);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Actions.Should().HaveCount(1);
        result.Value.DrillDownKeyPath.Should().NotBeNull();
        result.Value.DrillDownKeyPath.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public void RoundTrip_RenameAction_ProducesEquivalentRecipe()
    {
        // Arrange
        Recipe original = new()
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "col_a", NewName = "col_b" }],
        };

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("test");
        RenameColumnAction action = result.Value.Actions[0].Should().BeOfType<RenameColumnAction>().Subject;
        action.OldName.Should().Be("col_a");
        action.NewName.Should().Be("col_b");
    }

    [Fact]
    public void RoundTrip_DeleteAction_ProducesEquivalentRecipe()
    {
        // Arrange
        Recipe original = new()
        {
            Name = "test",
            Actions = [new DeleteColumnAction { ColumnName = "temp" }],
        };

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        DeleteColumnAction action = result.Value.Actions[0].Should().BeOfType<DeleteColumnAction>().Subject;
        action.ColumnName.Should().Be("temp");
    }

    [Fact]
    public void RoundTrip_CastAction_ProducesEquivalentRecipe()
    {
        // Arrange
        Recipe original = new()
        {
            Name = "test",
            Actions = [new CastColumnAction { ColumnName = "age", TargetType = ColumnType.WholeNumber }],
        };

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        CastColumnAction action = result.Value.Actions[0].Should().BeOfType<CastColumnAction>().Subject;
        action.ColumnName.Should().Be("age");
        action.TargetType.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void RoundTrip_FilterAction_ProducesEquivalentRecipe()
    {
        // Arrange
        Recipe original = new()
        {
            Name = "test",
            Actions = [FilterAction.Create("status", FilterOperator.Equals, ComparisonType.Text, "active").Value],
        };

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        FilterAction action = result.Value.Actions[0].Should().BeOfType<FilterAction>().Subject;
        action.ColumnName.Should().Be("status");
        action.Operator.Should().Be(FilterOperator.Equals);
        action.Value.Should().Be("active");
    }

    [Fact]
    public void RoundTrip_MultipleActions_PreservesAllActions()
    {
        // Arrange
        Recipe original = new()
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
        Result<Recipe> result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

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
        Recipe original = new()
        {
            Name = "full",
            Description = "A description",
            LastModified = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero),
            Actions = [],
        };

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("A description");
        result.Value.LastModified.Should().Be(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void RoundTrip_WithNullableFieldsAbsent_ReturnsNulls()
    {
        // Arrange
        Recipe original = new() { Name = "minimal", Actions = [] };

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().BeNull();
        result.Value.LastModified.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_BackslashInStringValue_PreservesValue()
    {
        // Arrange
        Recipe original = new()
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = @"C:\data\file", NewName = "output" }],
        };

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        RenameColumnAction action = result.Value.Actions[0].Should().BeOfType<RenameColumnAction>().Subject;
        action.OldName.Should().Be(@"C:\data\file");
    }

    [Fact]
    public void RoundTrip_BackslashFollowedByQuote_PreservesValue()
    {
        // Arrange
        Recipe original = new()
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "col\\\"name", NewName = "output" }],
        };

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        RenameColumnAction action = result.Value.Actions[0].Should().BeOfType<RenameColumnAction>().Subject;
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
        Recipe original = new()
        {
            Name = "test",
            Actions = [FilterAction.Create("col", op, comparisonType, value).Value],
        };

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        FilterAction action = result.Value.Actions[0].Should().BeOfType<FilterAction>().Subject;
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
        Recipe original = new()
        {
            Name = "test",
            Actions = [new CastColumnAction { ColumnName = "col", TargetType = columnType }],
        };

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        CastColumnAction action = result.Value.Actions[0].Should().BeOfType<CastColumnAction>().Subject;
        action.TargetType.Should().Be(columnType);
    }

    [Fact]
    public void RoundTrip_DrillDownKeyPath_WithLeadingBareIndex_PreservesAllSegments()
    {
        // Arrange: root-level array of arrays, drilling into [1][0] with no preceding key.
        Recipe original = new()
        {
            Name = "test",
            Actions = [new RenameColumnAction { OldName = "a", NewName = "b" }],
            DrillDownKeyPath =
            [
                new KeyPathSegment("[1]", KeyPathSegmentKind.Index),
                new KeyPathSegment("[0]", KeyPathSegmentKind.Index),
            ],
        };

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DrillDownKeyPath.Should().Equal(original.DrillDownKeyPath);
    }

    [Fact]
    public void RoundTrip_DrillDownKeyPath_EmptyPath_PreservesEmptyNotNull()
    {
        // Arrange: a root-level Full Aggregation DrillDown selecting a top-level element directly
        // has no KeyPath segments (see KeyPathTraverser.LastKeySegment).
        Recipe original = new() { Name = "test", Actions = [], DrillDownKeyPath = [] };

        // Act
        Result<Recipe> result = RecipeYamlParser.Parse(RecipeYamlSerializer.Serialize(original));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DrillDownKeyPath.Should().NotBeNull();
        result.Value.DrillDownKeyPath.Should().BeEmpty();
    }
}

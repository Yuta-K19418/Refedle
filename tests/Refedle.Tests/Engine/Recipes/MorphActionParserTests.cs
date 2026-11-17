using AwesomeAssertions;
using Refedle.Engine;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Recipes;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.Recipes;

public sealed class MorphActionParserTests
{
    [Fact]
    public void ParseAction_WithMissingTypeField_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "columnName", "Age" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Missing action type");
    }

    [Fact]
    public void ParseAction_WithUnknownType_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "explode" },
            { "columnName", "Age" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unknown action type: 'explode'");
    }

    [Fact]
    public void ParseAction_RenameAction_WithMissingOldName_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Rename" },
            { "newName", "NewAge" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'oldName'");
    }

    [Fact]
    public void ParseAction_RenameAction_WithMissingNewName_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Rename" },
            { "oldName", "Age" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'newName'");
    }

    [Fact]
    public void ParseAction_ValidRenameAction_ReturnsSuccess()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Rename" },
            { "oldName", "Age" },
            { "newName", "years" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        RenameColumnAction action = result.Value.Should().BeOfType<RenameColumnAction>().Subject;
        action.OldName.Should().Be("Age");
        action.NewName.Should().Be("years");
    }

    [Fact]
    public void ParseAction_ValidDeleteAction_ReturnsSuccess()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Delete" },
            { "columnName", "Age" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DeleteColumnAction action = result.Value.Should().BeOfType<DeleteColumnAction>().Subject;
        action.ColumnName.Should().Be("Age");
    }

    [Fact]
    public void ParseAction_DeleteAction_WithMissingColumnName_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new() { { "type", "Delete" } };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'columnName'");
    }

    [Fact]
    public void ParseAction_ValidCastAction_ReturnsSuccess()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Cast" },
            { "columnName", "Age" },
            { "targetType", "WholeNumber" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        CastColumnAction action = result.Value.Should().BeOfType<CastColumnAction>().Subject;
        action.ColumnName.Should().Be("Age");
        action.TargetType.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void ParseAction_CastAction_WithInvalidTargetType_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Cast" },
            { "columnName", "Age" },
            { "targetType", "NotAType" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid enum value for targetType");
    }

    [Fact]
    public void ParseAction_CastAction_WithWrongCaseTargetType_ReturnsFailure()
    {
        // Arrange — targetType is case-sensitive; "wholenumber" is not a valid value
        Dictionary<string, string> fields = new()
        {
            { "type", "Cast" },
            { "columnName", "Age" },
            { "targetType", "wholenumber" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid enum value for targetType");
    }

    [Theory]
    [InlineData("Contains", FilterOperator.Contains, ComparisonType.Text)]
    [InlineData("NotContains", FilterOperator.NotContains, ComparisonType.Text)]
    [InlineData("StartsWith", FilterOperator.StartsWith, ComparisonType.Text)]
    [InlineData("EndsWith", FilterOperator.EndsWith, ComparisonType.Text)]
    [InlineData("Equals", FilterOperator.Equals, ComparisonType.Text)]
    [InlineData("NotEquals", FilterOperator.NotEquals, ComparisonType.Text)]
    [InlineData("GreaterThan", FilterOperator.GreaterThan, ComparisonType.Number)]
    [InlineData("LessThan", FilterOperator.LessThan, ComparisonType.Number)]
    [InlineData("GreaterThanOrEqual", FilterOperator.GreaterThanOrEqual, ComparisonType.Number)]
    [InlineData("LessThanOrEqual", FilterOperator.LessThanOrEqual, ComparisonType.Number)]
    public void ParseAction_ValidFilterAction_ReturnsSuccess(
        string operatorStr, FilterOperator expectedOp, ComparisonType comparisonType)
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Filter" },
            { "columnName", "Age" },
            { "operator", operatorStr },
            { "comparisonType", comparisonType.ToString() },
            { "value", "30" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        FilterAction action = result.Value.Should().BeOfType<FilterAction>().Subject;
        action.ColumnName.Should().Be("Age");
        action.Operator.Should().Be(expectedOp);
        action.ComparisonType.Should().Be(comparisonType);
        action.Value.Should().Be("30");
    }

    [Fact]
    public void ParseAction_FilterAction_WithInvalidOperator_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Filter" },
            { "columnName", "Age" },
            { "operator", "EXPLODE" },
            { "value", "30" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid enum value for operator");
    }

    [Fact]
    public void ParseAction_FilterAction_WithWrongCaseOperator_ReturnsFailure()
    {
        // Arrange — operator is case-sensitive; "equals" is not a valid value
        Dictionary<string, string> fields = new()
        {
            { "type", "Filter" },
            { "columnName", "Age" },
            { "operator", "equals" },
            { "comparisonType", "Text" },
            { "value", "30" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid enum value for operator");
    }

    [Fact]
    public void ParseAction_FilterAction_WithMissingValue_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Filter" },
            { "columnName", "Age" },
            { "operator", "Equals" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'value'");
    }

    [Fact]
    public void ParseAction_ValidFillAction_ReturnsSuccess()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Fill" },
            { "columnName", "Email" },
            { "value", "REDACTED" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        FillColumnAction action = result.Value.Should().BeOfType<FillColumnAction>().Subject;
        action.ColumnName.Should().Be("Email");
        action.Value.Should().Be("REDACTED");
    }

    [Fact]
    public void ParseAction_FillAction_WithMissingColumnName_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Fill" },
            { "value", "REDACTED" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'columnName'");
    }

    [Fact]
    public void ParseAction_FillAction_WithMissingValue_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Fill" },
            { "columnName", "Email" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'value'");
    }

    [Fact]
    public void ParseAction_FillAction_WithEmptyValue_ReturnsSuccess()
    {
        // Arrange — empty string is a valid fill value (e.g., blank-out a column)
        Dictionary<string, string> fields = new()
        {
            { "type", "Fill" },
            { "columnName", "Email" },
            { "value", "" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        FillColumnAction action = result.Value.Should().BeOfType<FillColumnAction>().Subject;
        action.Value.Should().Be("");
    }

    [Fact]
    public void ParseAction_ValidFormatTimestampAction_ReturnsSuccess()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "FormatTimestamp" },
            { "columnName", "CreatedAt" },
            { "targetFormat", "yyyy/MM/dd" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        FormatTimestampAction action = result.Value.Should().BeOfType<FormatTimestampAction>().Subject;
        action.ColumnName.Should().Be("CreatedAt");
        action.TargetFormat.Should().Be("yyyy/MM/dd");
    }

    [Fact]
    public void ParseAction_FormatTimestampAction_WithMissingColumnName_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "FormatTimestamp" },
            { "targetFormat", "yyyy/MM/dd" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'columnName'");
    }

    [Fact]
    public void ParseAction_FormatTimestampAction_WithMissingTargetFormat_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "FormatTimestamp" },
            { "columnName", "CreatedAt" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'targetFormat'");
    }

    [Fact]
    public void ParseAction_FormatTimestampAction_WithEmptyTargetFormat_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "FormatTimestamp" },
            { "columnName", "CreatedAt" },
            { "targetFormat", "" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("targetFormat");
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void ParseAction_FilterAction_WithMissingComparisonType_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Filter" },
            { "columnName", "Age" },
            { "operator", "Equals" },
            { "value", "30" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'comparisonType'");
    }

    [Fact]
    public void ParseAction_FilterAction_WithInvalidComparisonType_ReturnsFailure()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Filter" },
            { "columnName", "Age" },
            { "operator", "Equals" },
            { "comparisonType", "NotAType" },
            { "value", "30" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid enum value for comparisonType");
    }

    [Fact]
    public void ParseAction_FilterAction_WithWrongCaseComparisonType_ReturnsFailure()
    {
        // Arrange — comparisonType is case-sensitive; "text" is not a valid value
        Dictionary<string, string> fields = new()
        {
            { "type", "Filter" },
            { "columnName", "Age" },
            { "operator", "Equals" },
            { "comparisonType", "text" },
            { "value", "30" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid enum value for comparisonType");
    }

    [Fact]
    public void ParseAction_FilterAction_WithInvalidOperatorCombination_ReturnsFailure()
    {
        // Arrange — Contains is not valid for a Number comparison type
        Dictionary<string, string> fields = new()
        {
            { "type", "Filter" },
            { "columnName", "Age" },
            { "operator", "Contains" },
            { "comparisonType", "Number" },
            { "value", "30" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ParseAction_FilterAction_WithUnparseableValue_ReturnsFailure()
    {
        // Arrange — GreaterThan/Number requires a parseable numeric value
        Dictionary<string, string> fields = new()
        {
            { "type", "Filter" },
            { "columnName", "Age" },
            { "operator", "GreaterThan" },
            { "comparisonType", "Number" },
            { "value", "abc" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ParseAction_FilterAction_WithValidComparisonType_PreservesComparisonType()
    {
        // Arrange
        Dictionary<string, string> fields = new()
        {
            { "type", "Filter" },
            { "columnName", "Age" },
            { "operator", "GreaterThan" },
            { "comparisonType", "Timestamp" },
            { "value", "2025-01-01" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsSuccess.Should().BeTrue();
        FilterAction action = result.Value.Should().BeOfType<FilterAction>().Subject;
        action.ComparisonType.Should().Be(ComparisonType.Timestamp);
    }

    [Fact]
    public void ParseAction_FilterAction_WithNumericComparisonType_ReturnsFailure()
    {
        // Arrange — '999' parses as a number to an undefined ComparisonType value
        Dictionary<string, string> fields = new()
        {
            { "type", "Filter" },
            { "columnName", "Age" },
            { "operator", "Equals" },
            { "comparisonType", "999" },
            { "value", "30" },
        };

        // Act
        Result<IMorphAction> result = MorphActionParser.ParseAction(fields);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}

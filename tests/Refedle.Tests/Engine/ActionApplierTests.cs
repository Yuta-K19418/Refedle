using AwesomeAssertions;
using Refedle.Engine;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine;

public sealed class ActionApplierTests
{
    [Fact]
    public void BuildOutputSchema_WithNoActions_ReturnsAllColumnsAndNoFilters()
    {
        // Arrange

        // Act
        var result = ActionApplier.BuildOutputSchema(["A"], []).Value;

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[0].OutputName.Should().Be("A");
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithRenameAction_UpdatesOutputName()
    {
        // Arrange
        MorphAction[] actions = [new RenameColumnAction { OldName = "A", NewName = "RenamedA" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A", "B"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(2);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[0].OutputName.Should().Be("RenamedA");
        result.Columns[1].SourceName.Should().Be("B");
        result.Columns[1].OutputName.Should().Be("B");
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithRenameAction_OnNonExistentColumn_SkipsSilently()
    {
        // Arrange
        MorphAction[] actions = [new RenameColumnAction { OldName = "NonExistent", NewName = "NewName" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[0].OutputName.Should().Be("A");
    }

    [Fact]
    public void BuildOutputSchema_WithDeleteAction_RemovesColumn()
    {
        // Arrange
        MorphAction[] actions = [new DeleteColumnAction { ColumnName = "A" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A", "B"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("B");
        result.Columns[0].OutputName.Should().Be("B");
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithDeleteAction_OnNonExistentColumn_SkipsSilently()
    {
        // Arrange
        MorphAction[] actions = [new DeleteColumnAction { ColumnName = "NonExistent" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A", "B"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(2);
    }

    [Fact]
    public void BuildOutputSchema_WithFilterAction_AddsBatchFilterSpec()
    {
        // Arrange
        MorphAction[] actions = [FilterAction.Create("A", FilterOperator.Equals, ComparisonType.Text, "test").Value];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Filters.Should().HaveCount(1);
        result.Filters[0].SourceColumnIndex.Should().Be(0);
        result.Filters[0].ComparisonType.Should().Be(ComparisonType.Text);
        result.Filters[0].Operator.Should().Be(FilterOperator.Equals);
        result.Filters[0].Value.Should().Be("test");
    }

    [Fact]
    public void BuildOutputSchema_WithFilterAction_OnNonExistentColumn_SkipsSilently()
    {
        // Arrange
        MorphAction[] actions = [FilterAction.Create("NonExistent", FilterOperator.Equals, ComparisonType.Text, "test").Value];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A"], actions).Value;

        // Assert
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithFilterOnDeletedColumn_SkipsSilently()
    {
        // Arrange
        MorphAction[] actions =
        [
            new DeleteColumnAction { ColumnName = "B" },
            FilterAction.Create("B", FilterOperator.Equals, ComparisonType.Text, "test").Value,
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A", "B"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithCastAction_DoesNotAffectColumnInclusion()
    {
        // Arrange
        MorphAction[] actions = [new CastColumnAction { ColumnName = "A", TargetType = ColumnType.WholeNumber }];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[0].OutputName.Should().Be("A");
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithCastThenFilterOnSameColumn_UsesComparisonTypeNotColumnType()
    {
        // Arrange
        MorphAction[] actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = ColumnType.WholeNumber },
            FilterAction.Create("A", FilterOperator.GreaterThan, ComparisonType.Number, "10").Value,
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A"], actions).Value;

        // Assert — the filter resolves from the action's ComparisonType, not the cast type.
        result.Filters.Should().HaveCount(1);
        result.Filters[0].SourceColumnIndex.Should().Be(0);
        result.Filters[0].ComparisonType.Should().Be(ComparisonType.Number);
    }

    [Fact]
    public void BuildOutputSchema_PreservesColumnOrder()
    {
        // Arrange
        MorphAction[] actions = [new DeleteColumnAction { ColumnName = "B" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A", "B", "C"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(2);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[1].SourceName.Should().Be("C");
    }

    [Fact]
    public void BuildOutputSchema_WithCastAction_OnNonExistentColumn_SkipsSilently()
    {
        // Arrange
        MorphAction[] actions = [new CastColumnAction { ColumnName = "NonExistent", TargetType = ColumnType.WholeNumber }];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithMultipleFilterActions_AddsAllFilterSpecs()
    {
        // Arrange
        MorphAction[] actions =
        [
            FilterAction.Create("A", FilterOperator.Equals, ComparisonType.Text, "value1").Value,
            FilterAction.Create("B", FilterOperator.Contains, ComparisonType.Text, "value2").Value,
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A", "B"], actions).Value;

        // Assert
        result.Filters.Should().HaveCount(2);
        result.Filters[0].SourceColumnIndex.Should().Be(0);
        result.Filters[1].SourceColumnIndex.Should().Be(1);
    }

    [Fact]
    public void BuildOutputSchema_WithChainedRenameActions_AppliesAllRenamesInOrder()
    {
        // Arrange
        MorphAction[] actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "B" },
            new RenameColumnAction { OldName = "B", NewName = "C" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[0].OutputName.Should().Be("C");
    }

    [Fact]
    public void BuildOutputSchema_WithNullColumnNames_ThrowsArgumentNullException()
    {
        // Arrange
        MorphAction[] actions = [];

        // Act
        var act = () => ActionApplier.BuildOutputSchema(null!, actions);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildOutputSchema_WithNullActions_ThrowsArgumentNullException()
    {
        // Arrange

        // Act
        var act = () => ActionApplier.BuildOutputSchema(["A"], null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildOutputSchema_WithAllColumnsDeleted_ReturnsEmptyColumnsAndNoFilters()
    {
        // Arrange — delete the only column; output schema should have no columns and no filters
        MorphAction[] actions = [new DeleteColumnAction { ColumnName = "A" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A"], actions).Value;

        // Assert
        result.Columns.Should().BeEmpty();
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithCastRenameThenFilter_ResolvesByRenamedNameAndComparisonType()
    {
        // Arrange
        MorphAction[] actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = ColumnType.WholeNumber },
            new RenameColumnAction { OldName = "A", NewName = "RenamedA" },
            FilterAction.Create("RenamedA", FilterOperator.GreaterThan, ComparisonType.Number, "10").Value,
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A", "B"], actions).Value;

        // Assert — the filter carries the action's ComparisonType and resolves by renamed name.
        result.Columns.Should().HaveCount(2);
        result.Columns[0].SourceName.Should().Be("A");
        result.Columns[0].OutputName.Should().Be("RenamedA");
        result.Filters.Should().HaveCount(1);
        result.Filters[0].ComparisonType.Should().Be(ComparisonType.Number);
    }

    // -------------------------------------------------------------------------
    // FillColumnAction
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildOutputSchema_WithFillAction_SingleColumn_AttachesTransformToColumn()
    {
        // Arrange
        MorphAction[] actions = [new FillColumnAction { ColumnName = "Email", Value = "REDACTED" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(["Email"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("Email");
        result.Columns[0].OutputName.Should().Be("Email");
        result.Columns[0].Transform.Should().BeOfType<FillSpec>().Which.Value.Should().Be("REDACTED");
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithFillAction_OnNonExistentColumn_SkipsSilently()
    {
        // Arrange
        MorphAction[] actions = [new FillColumnAction { ColumnName = "NonExistent", Value = "FILL" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].Transform.Should().BeNull();
    }

    [Fact]
    public void BuildOutputSchema_WithMultipleActions_IncludingFill_AppliesAllCorrectly()
    {
        // Arrange
        MorphAction[] actions =
        [
            new RenameColumnAction { OldName = "Name", NewName = "FullName" },
            new FillColumnAction { ColumnName = "Email", Value = "REDACTED" },
            new DeleteColumnAction { ColumnName = "Age" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(["Name", "Email", "Age"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(2);
        result.Columns[0].SourceName.Should().Be("Name");
        result.Columns[0].OutputName.Should().Be("FullName");
        result.Columns[0].Transform.Should().BeNull();
        result.Columns[1].SourceName.Should().Be("Email");
        result.Columns[1].Transform.Should().BeOfType<FillSpec>().Which.Value.Should().Be("REDACTED");
    }

    [Fact]
    public void BuildOutputSchema_WithRenameAndFill_FillTargetsRenamedColumn()
    {
        // Arrange — rename first, then fill using the new name; transform must be attached
        MorphAction[] actions =
        [
            new RenameColumnAction { OldName = "Email", NewName = "EmailAddress" },
            new FillColumnAction { ColumnName = "EmailAddress", Value = "REDACTED" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(["Email"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].SourceName.Should().Be("Email");
        result.Columns[0].OutputName.Should().Be("EmailAddress");
        result.Columns[0].Transform.Should().BeOfType<FillSpec>().Which.Value.Should().Be("REDACTED");
    }

    [Fact]
    public void BuildOutputSchema_WithTwoFillsOnSameColumn_LastFillWins()
    {
        // Arrange — two consecutive fills on the same column; second value must win
        MorphAction[] actions =
        [
            new FillColumnAction { ColumnName = "Status", Value = "FIRST" },
            new FillColumnAction { ColumnName = "Status", Value = "SECOND" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(["Status"], actions).Value;

        // Assert
        result.Columns.Should().HaveCount(1);
        result.Columns[0].Transform.Should().BeOfType<FillSpec>().Which.Value.Should().Be("SECOND");
    }

    [Fact]
    public void BuildOutputSchema_WithFillAction_OnDeletedColumn_SkipsSilently()
    {
        // Arrange — delete the column first; subsequent fill on the same (now removed) column is a no-op
        MorphAction[] actions =
        [
            new DeleteColumnAction { ColumnName = "Email" },
            new FillColumnAction { ColumnName = "Email", Value = "REDACTED" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(["Email"], actions).Value;

        // Assert
        result.Columns.Should().BeEmpty();
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void BuildOutputSchema_WithFillThenDelete_ColumnIsRemoved()
    {
        // Arrange — fill column, then delete it; column should not appear in output
        MorphAction[] actions =
        [
            new FillColumnAction { ColumnName = "A", Value = "FILLED" },
            new DeleteColumnAction { ColumnName = "A" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A"], actions).Value;

        // Assert
        result.Columns.Should().BeEmpty();
        result.Filters.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // FormatTimestampAction
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildOutputSchema_WithFormatTimestampAction_OnTimestampColumn_AttachesTransformToColumn()
    {
        // Arrange
        MorphAction[] actions = [new FormatTimestampAction { ColumnName = "CreatedAt", TargetFormat = "yyyy/MM/dd" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(["CreatedAt"], actions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Should().HaveCount(1);
        result.Value.Columns[0].SourceName.Should().Be("CreatedAt");
        result.Value.Columns[0].Transform.Should().BeOfType<TimestampFormatSpec>()
            .Which.TargetFormat.Should().Be("yyyy/MM/dd");
    }

    [Fact]
    public void BuildOutputSchema_WithFormatTimestampAction_OnNonTimestampColumn_AttachesTransform()
    {
        // Arrange — there is no schema-time Timestamp gate anymore, so any column
        // accepts the transform (per-row parsing happens at read time, unchanged).
        MorphAction[] actions = [new FormatTimestampAction { ColumnName = "Name", TargetFormat = "yyyy/MM/dd" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(["Name"], actions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns[0].Transform.Should().BeOfType<TimestampFormatSpec>()
            .Which.TargetFormat.Should().Be("yyyy/MM/dd");
    }

    [Fact]
    public void BuildOutputSchema_WithFormatTimestampAction_OnNonExistentColumn_SkipsSilently()
    {
        // Arrange
        MorphAction[] actions = [new FormatTimestampAction { ColumnName = "NonExistent", TargetFormat = "yyyy/MM/dd" }];

        // Act
        var result = ActionApplier.BuildOutputSchema(["A"], actions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Should().HaveCount(1);
        result.Value.Columns[0].Transform.Should().BeNull();
    }

    [Fact]
    public void BuildOutputSchema_WithCastToTimestampThenFormat_AppliesCorrectly()
    {
        // Arrange
        MorphAction[] actions =
        [
            new CastColumnAction { ColumnName = "Date", TargetType = ColumnType.Timestamp },
            new FormatTimestampAction { ColumnName = "Date", TargetFormat = "dd-MM-yyyy" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(["Date"], actions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Should().HaveCount(1);
        result.Value.Columns[0].Transform.Should().BeOfType<TimestampFormatSpec>()
            .Which.TargetFormat.Should().Be("dd-MM-yyyy");
    }

    [Fact]
    public void BuildOutputSchema_WithFormatTimestampAction_OnDeletedColumn_SkipsSilently()
    {
        // Arrange — delete column first; subsequent format_timestamp on same (now removed) column is a no-op
        MorphAction[] actions =
        [
            new DeleteColumnAction { ColumnName = "CreatedAt" },
            new FormatTimestampAction { ColumnName = "CreatedAt", TargetFormat = "yyyy/MM/dd" },
        ];

        // Act
        var result = ActionApplier.BuildOutputSchema(["CreatedAt"], actions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Should().BeEmpty();
        result.Value.Filters.Should().BeEmpty();
    }
}

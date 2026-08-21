using AwesomeAssertions;
using Refedle.App;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Tests.App;

public sealed class AppStateTests
{
    private static AppState CreateStateWithDrillDown(MorphAction drillDownAction) =>
        new()
        {
            DrillDown = new DrillDownState(
                [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
                new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] },
                ViewMode.JsonLinesTree,
                ActionStack: [drillDownAction]),
        };

    [Fact]
    public void AddMorphAction_SingleAction_AddsToStack()
    {
        // Arrange
        using var state = new AppState();
        var action = new RenameColumnAction { OldName = "foo", NewName = "bar" };

        // Act
        state.AddMorphAction(action);

        // Assert
        state.ActionStack.Should().ContainSingle();
        state.ActionStack[0].Should().Be(action);
    }

    [Fact]
    public void AddMorphAction_MultipleActions_PreservesOrder()
    {
        // Arrange
        using var state = new AppState();
        var action1 = new RenameColumnAction { OldName = "a", NewName = "b" };
        var action2 = new DeleteColumnAction { ColumnName = "c" };
        var action3 = new CastColumnAction { ColumnName = "d", TargetType = ColumnType.WholeNumber };

        // Act
        state.AddMorphAction(action1);
        state.AddMorphAction(action2);
        state.AddMorphAction(action3);

        // Assert
        state.ActionStack.Should().HaveCount(3);
        state.ActionStack[0].Should().Be(action1);
        state.ActionStack[1].Should().Be(action2);
        state.ActionStack[2].Should().Be(action3);
    }

    [Fact]
    public void AddMorphAction_DoesNotMutateOriginalList()
    {
        // Arrange
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        var originalList = state.ActionStack;

        // Act
        state.AddMorphAction(new DeleteColumnAction { ColumnName = "c" });

        // Assert
        originalList.Should().ContainSingle();
        state.ActionStack.Should().HaveCount(2);
    }

    [Fact]
    public void ClearMorphActions_WithActions_ClearsActionStack()
    {
        // Arrange
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        state.AddMorphAction(new DeleteColumnAction { ColumnName = "c" });

        // Act
        state.ClearMorphActions();

        // Assert
        state.ActionStack.Should().BeEmpty();
    }

    [Fact]
    public void ClearMorphActions_WithEmptyStack_StackRemainsEmpty()
    {
        // Arrange
        using var state = new AppState();

        // Act
        state.ClearMorphActions();

        // Assert
        state.ActionStack.Should().BeEmpty();
    }

    [Fact]
    public void ClearMorphActions_DoesNotMutatePreviousStackReference()
    {
        // Arrange
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        var originalList = state.ActionStack;

        // Act
        state.ClearMorphActions();

        // Assert
        originalList.Should().HaveCount(1);
        state.ActionStack.Should().BeEmpty();
    }

    [Fact]
    public void SetActionStack_ReplacesEntireStack()
    {
        // Arrange
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        var replacement = new MorphAction[] { new DeleteColumnAction { ColumnName = "c" } };

        // Act
        state.SetActionStack(replacement);

        // Assert
        state.ActionStack.Should().HaveCount(1);
        state.ActionStack[0].Should().Be(replacement[0]);
    }

    [Fact]
    public void SetActionStack_WithEmptyList_ClearsStack()
    {
        // Arrange
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });

        // Act
        state.SetActionStack([]);

        // Assert
        state.ActionStack.Should().BeEmpty();
    }

    [Fact]
    public void AddMorphAction_WithActiveDrillDown_DoesNotAffectDrillDownActionStack()
    {
        // Arrange
        var drillDownAction = new RenameColumnAction { OldName = "x", NewName = "y" };
        using var state = CreateStateWithDrillDown(drillDownAction);

        // Act
        state.AddMorphAction(new DeleteColumnAction { ColumnName = "c" });

        // Assert
        state.ActionStack.Should().ContainSingle();
        var drillDown = state.DrillDown.Should().BeOfType<DrillDownState>().Which;
        drillDown.ActionStack.Should().Equal(drillDownAction);
    }

    [Fact]
    public void ClearMorphActions_WithActiveDrillDown_DoesNotAffectDrillDownActionStack()
    {
        // Arrange
        var drillDownAction = new RenameColumnAction { OldName = "x", NewName = "y" };
        using var state = CreateStateWithDrillDown(drillDownAction);
        state.AddMorphAction(new DeleteColumnAction { ColumnName = "c" });

        // Act
        state.ClearMorphActions();

        // Assert
        state.ActionStack.Should().BeEmpty();
        var drillDown = state.DrillDown.Should().BeOfType<DrillDownState>().Which;
        drillDown.ActionStack.Should().Equal(drillDownAction);
    }

    [Fact]
    public void SetActionStack_WithActiveDrillDown_LeavesDrillDownActionStackUntouched()
    {
        // Arrange
        var drillDownAction = new RenameColumnAction { OldName = "x", NewName = "y" };
        using var state = CreateStateWithDrillDown(drillDownAction);
        var replacement = new MorphAction[] { new DeleteColumnAction { ColumnName = "base" } };

        // Act
        state.SetActionStack(replacement);

        // Assert
        state.ActionStack.Should().Equal(replacement);
        var drillDown = state.DrillDown.Should().BeOfType<DrillDownState>().Which;
        drillDown.ActionStack.Should().Equal(drillDownAction);
    }

    [Fact]
    public void CurrentKeyPath_Default_IsEmpty()
    {
        // Arrange
        using var state = new AppState();

        // Act
        var result = state.CurrentKeyPath;

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void JsonObjectEntries_Default_IsNull()
    {
        // Arrange
        using var state = new AppState();

        // Act
        var result = state.JsonObjectEntries;

        // Assert
        result.Should().BeNull();
    }
}

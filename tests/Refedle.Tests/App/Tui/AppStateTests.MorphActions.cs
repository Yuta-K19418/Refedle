using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui;

namespace Refedle.Tests.App.Tui;

public sealed partial class AppStateTests
{
    [Fact]
    public void AddMorphAction_SingleAction_AddsToStack()
    {
        // Arrange
        using AppState state = new();
        RenameColumnAction action = new() { OldName = "foo", NewName = "bar" };

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
        using AppState state = new();
        RenameColumnAction action1 = new() { OldName = "a", NewName = "b" };
        DeleteColumnAction action2 = new() { ColumnName = "c" };
        CastColumnAction action3 = new() { ColumnName = "d", TargetType = ColumnType.WholeNumber };

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
        using AppState state = new();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        IReadOnlyList<IMorphAction> originalList = state.ActionStack;

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
        using AppState state = new();
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
        using AppState state = new();

        // Act
        state.ClearMorphActions();

        // Assert
        state.ActionStack.Should().BeEmpty();
    }

    [Fact]
    public void ClearMorphActions_DoesNotMutatePreviousStackReference()
    {
        // Arrange
        using AppState state = new();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        IReadOnlyList<IMorphAction> originalList = state.ActionStack;

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
        using AppState state = new();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        IMorphAction[] replacement = [new DeleteColumnAction { ColumnName = "c" }];

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
        using AppState state = new();
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
        RenameColumnAction drillDownAction = new() { OldName = "x", NewName = "y" };
        using AppState state = CreateStateWithDrillDown(drillDownAction);

        // Act
        state.AddMorphAction(new DeleteColumnAction { ColumnName = "c" });

        // Assert
        state.ActionStack.Should().ContainSingle();
        DrillDownState drillDown = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
        drillDown.ActionStack.Should().Equal(drillDownAction);
    }

    [Fact]
    public void ClearMorphActions_WithActiveDrillDown_DoesNotAffectDrillDownActionStack()
    {
        // Arrange
        RenameColumnAction drillDownAction = new() { OldName = "x", NewName = "y" };
        using AppState state = CreateStateWithDrillDown(drillDownAction);
        state.AddMorphAction(new DeleteColumnAction { ColumnName = "c" });

        // Act
        state.ClearMorphActions();

        // Assert
        state.ActionStack.Should().BeEmpty();
        DrillDownState drillDown = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
        drillDown.ActionStack.Should().Equal(drillDownAction);
    }

    [Fact]
    public void SetActionStack_WithActiveDrillDown_LeavesDrillDownActionStackUntouched()
    {
        // Arrange
        RenameColumnAction drillDownAction = new() { OldName = "x", NewName = "y" };
        using AppState state = CreateStateWithDrillDown(drillDownAction);
        IMorphAction[] replacement = [new DeleteColumnAction { ColumnName = "base" }];

        // Act
        state.SetActionStack(replacement);

        // Assert
        state.ActionStack.Should().Equal(replacement);
        DrillDownState drillDown = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
        drillDown.ActionStack.Should().Equal(drillDownAction);
    }

    [Fact]
    public void HasUnsavedChanges_Default_IsFalse()
    {
        // Arrange
        using AppState state = new();

        // Act
        bool result = state.HasUnsavedChanges;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AddMorphAction_SetsUnsavedChanges()
    {
        // Arrange
        using AppState state = new();

        // Act
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });

        // Assert
        state.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void ClearMorphActions_WhenSavedStackIsNonEmpty_SetsUnsavedChanges()
    {
        // Arrange
        using AppState state = new();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        state.MarkRecipeSaved();

        // Act
        state.ClearMorphActions();

        // Assert
        state.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void ClearMorphActions_WhenStackAlreadyEmpty_LeavesStateClean()
    {
        // Arrange
        using AppState state = new();
        long revisionBefore = state.Revision;

        // Act
        state.ClearMorphActions();

        // Assert
        state.HasUnsavedChanges.Should().BeFalse();
        state.Revision.Should().Be(revisionBefore);
    }

    [Fact]
    public void SetActionStack_SetsUnsavedChanges()
    {
        // Arrange
        using AppState state = new();

        // Act
        state.SetActionStack([]);

        // Assert
        state.HasUnsavedChanges.Should().BeTrue();
    }
}

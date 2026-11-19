using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.TestHelpers.App.Tui;

namespace Refedle.Tests.App.Tui;

public sealed partial class AppStateTests
{
    [Fact]
    public void CurrentKeyPath_Default_IsEmpty()
    {
        // Arrange
        using AppState state = new();

        // Act
        IReadOnlyList<KeyPathSegment> result = state.CurrentKeyPath;

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void DrillDownState_WithInitialActions_DefaultsToClean()
    {
        // Arrange — the drillDownAction comes from a loaded recipe, not an unsaved edit
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        bool hasUnsavedChanges = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which.HasUnsavedChanges;

        // Assert
        hasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void EnterFocusedTable_WithSession_EntersFocusedTableModeWithThatSession()
    {
        // Arrange
        using AppState state = new();
        DrillDownState drillDown = CreateDrillDownState(new DeleteColumnAction { ColumnName = "c" });

        // Act
        state.EnterFocusedTable(drillDown);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        state.TryGetDrillDown(out DrillDownState? session).Should().BeTrue();
        session.Should().BeSameAs(drillDown);
    }

    [Fact]
    public void EnterFocusedTable_WithSession_LeavesSchemaRowIndexerAndActionStackUntouched()
    {
        // Arrange
        using AppState state = new();
        AppStateTestRowIndexer indexer = new("data.csv");
        TableSchema schema = CreateSchema();
        state.CompleteCsvLoad(indexer, schema);
        state.AddMorphAction(RootAction);

        // Act
        state.EnterFocusedTable(CreateDrillDownState(new DeleteColumnAction { ColumnName = "c" }));

        // Assert
        state.Schema.Should().BeSameAs(schema);
        state.RowIndexer.Should().BeSameAs(indexer);
        state.ActionStack.Should().Equal(RootAction);
    }

    [Fact]
    public void SetCurrentKeyPath_WithPath_StoresPath()
    {
        // Arrange
        using AppState state = new();
        IReadOnlyList<KeyPathSegment> path = [new KeyPathSegment("orders", KeyPathSegmentKind.Key)];

        // Act
        state.SetCurrentKeyPath(path);

        // Assert
        state.CurrentKeyPath.Should().BeSameAs(path);
    }

    [Fact]
    public void SetCurrentKeyPath_WithEmptyPath_ClearsPathAndLeavesModeUntouched()
    {
        // Arrange
        using AppState state = CreatePopulatedState();

        // Act
        state.SetCurrentKeyPath([]);

        // Assert
        state.CurrentKeyPath.Should().BeEmpty();
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
    }

    [Fact]
    public void TryGetDrillDown_WhenFocusedTableWithSession_ReturnsTrueAndSession()
    {
        // Arrange
        RenameColumnAction drillDownAction = new() { OldName = "x", NewName = "y" };
        using AppState state = CreateStateWithDrillDown(drillDownAction);

        // Act
        bool found = state.TryGetDrillDown(out DrillDownState? drillDown);

        // Assert
        found.Should().BeTrue();
        DrillDownState actual = drillDown.Should().BeOfType<DrillDownState>().Which;
        actual.ActionStack.Should().Equal(drillDownAction);
    }

    [Fact]
    public void TryGetDrillDown_WhenNoSessionStarted_ReturnsFalse()
    {
        // Arrange
        using AppState state = new();

        // Act
        bool found = state.TryGetDrillDown(out DrillDownState? drillDown);

        // Assert
        found.Should().BeFalse();
        drillDown.Should().BeNull();
    }

    [Fact]
    public void TryGetDrillDown_AfterPlaceholderModeReplacedFocusedTable_ReturnsFalse()
    {
        // Arrange
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });
        state.EnterPlaceholderMode();

        // Act
        bool found = state.TryGetDrillDown(out DrillDownState? drillDown);

        // Assert
        found.Should().BeFalse();
        drillDown.Should().BeNull();
    }

    [Fact]
    public void ExitFocusedTable_WithSession_RestoresGivenModeAndEndsSession()
    {
        // Arrange
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        state.ExitFocusedTable(ViewMode.JsonLinesTree);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
        state.TryGetDrillDown(out _).Should().BeFalse();
    }

    [Fact]
    public void AddDrillDownAction_WithSession_AppendsToSessionAndMarksItUnsaved()
    {
        // Arrange
        RenameColumnAction existing = new() { OldName = "x", NewName = "y" };
        DeleteColumnAction added = new() { ColumnName = "c" };
        using AppState state = CreateStateWithDrillDown(existing);

        // Act
        state.AddDrillDownAction(added);

        // Assert
        state.TryGetDrillDown(out DrillDownState? drillDown).Should().BeTrue();
        DrillDownState actual = drillDown.Should().BeOfType<DrillDownState>().Which;
        actual.ActionStack.Should().Equal(existing, added);
        actual.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void AddDrillDownAction_WithSession_LeavesRootActionStackUnsavedFlagAndModeUntouched()
    {
        // Arrange
        using AppState state = CreateSavedRootState(CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" }));

        // Act
        state.AddDrillDownAction(new DeleteColumnAction { ColumnName = "c" });

        // Assert
        state.ActionStack.Should().Equal(RootAction);
        state.HasUnsavedChanges.Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
    }

    [Fact]
    public void AddDrillDownAction_AfterPlaceholderModeReplacedFocusedTable_LeavesNoSession()
    {
        // Arrange
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });
        state.EnterPlaceholderMode();

        // Act
        state.AddDrillDownAction(new DeleteColumnAction { ColumnName = "c" });

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
    }

    [Fact]
    public void AddDrillDownAction_WithoutSession_LeavesModeRootActionStackAndUnsavedFlagUntouched()
    {
        // Arrange
        using AppState state = CreateSavedRootState(new AppState());
        state.EnterJsonLinesTree();

        // Act
        state.AddDrillDownAction(new DeleteColumnAction { ColumnName = "c" });

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
        state.ActionStack.Should().Equal(RootAction);
        state.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void ClearDrillDownActions_WithSession_EmptiesSessionStackAndMarksItUnsaved()
    {
        // Arrange
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        state.ClearDrillDownActions();

        // Assert
        state.TryGetDrillDown(out DrillDownState? drillDown).Should().BeTrue();
        DrillDownState actual = drillDown.Should().BeOfType<DrillDownState>().Which;
        actual.ActionStack.Should().BeEmpty();
        actual.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void ClearDrillDownActions_WithSession_LeavesRootActionStackUnsavedFlagAndModeUntouched()
    {
        // Arrange
        using AppState state = CreateSavedRootState(CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" }));

        // Act
        state.ClearDrillDownActions();

        // Assert
        state.ActionStack.Should().Equal(RootAction);
        state.HasUnsavedChanges.Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
    }

    [Fact]
    public void ClearDrillDownActions_AfterPlaceholderModeReplacedFocusedTable_LeavesNoSession()
    {
        // Arrange
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });
        state.EnterPlaceholderMode();

        // Act
        state.ClearDrillDownActions();

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
    }

    [Fact]
    public void ClearDrillDownActions_WithoutSession_LeavesModeRootActionStackAndUnsavedFlagUntouched()
    {
        // Arrange
        using AppState state = CreateSavedRootState(new AppState());
        state.EnterJsonLinesTree();

        // Act
        state.ClearDrillDownActions();

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
        state.ActionStack.Should().Equal(RootAction);
        state.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void MarkDrillDownSaved_WithUnsavedSession_ClearsUnsavedFlagAndKeepsActions()
    {
        // Arrange
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });
        state.AddDrillDownAction(new DeleteColumnAction { ColumnName = "c" });
        state.TryGetDrillDown(out DrillDownState? before).Should().BeTrue();
        DrillDownState actualBefore = before.Should().BeOfType<DrillDownState>().Which;

        // Act
        state.MarkDrillDownSaved();

        // Assert
        state.TryGetDrillDown(out DrillDownState? after).Should().BeTrue();
        DrillDownState actualAfter = after.Should().BeOfType<DrillDownState>().Which;
        actualAfter.HasUnsavedChanges.Should().BeFalse();
        actualAfter.ActionStack.Should().Equal(actualBefore.ActionStack);
    }

    [Fact]
    public void MarkDrillDownSaved_WithSession_LeavesRootActionStackUnsavedFlagAndModeUntouched()
    {
        // Arrange
        using AppState state = CreateSavedRootState(CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" }));
        state.AddMorphAction(new DeleteColumnAction { ColumnName = "again" });

        // Act
        state.MarkDrillDownSaved();

        // Assert
        state.ActionStack.Should().HaveCount(2);
        state.HasUnsavedChanges.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
    }

    [Fact]
    public void MarkDrillDownSaved_AfterPlaceholderModeReplacedFocusedTable_LeavesNoSession()
    {
        // Arrange
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });
        state.AddDrillDownAction(new DeleteColumnAction { ColumnName = "c" });
        state.EnterPlaceholderMode();

        // Act
        state.MarkDrillDownSaved();

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
    }

    [Fact]
    public void MarkDrillDownSaved_WithoutSession_LeavesModeRootActionStackAndUnsavedFlagUntouched()
    {
        // Arrange
        using AppState state = CreateSavedRootState(new AppState());
        state.AddMorphAction(new DeleteColumnAction { ColumnName = "again" });
        state.EnterJsonLinesTree();

        // Act
        state.MarkDrillDownSaved();

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
        state.ActionStack.Should().HaveCount(2);
        state.HasUnsavedChanges.Should().BeTrue();
    }
}

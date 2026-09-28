using AwesomeAssertions;
using Refedle.App;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Tests.App;

public sealed class AppStateTests
{
    private static readonly MorphAction RootAction = new RenameColumnAction { OldName = "root", NewName = "root_renamed" };

    private static TableSchema CreateSchema(string columnName = "col1") =>
        new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = columnName, Type = ColumnType.Text }] };

    private static DrillDownState CreateDrillDownState(MorphAction drillDownAction) =>
        new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            CreateSchema(),
            ViewMode.JsonLinesTree,
            KeyPath: [],
            ActionStack: [drillDownAction]);

    private static AppState CreateStateWithDrillDown(MorphAction drillDownAction)
    {
        var state = new AppState();
        state.EnterFocusedTable(CreateDrillDownState(drillDownAction));
        return state;
    }

    // Root state whose Action Stack is saved, so a change to either root property stands out.
    private static AppState CreateSavedRootState(AppState state)
    {
        state.AddMorphAction(RootAction);
        state.MarkRecipeSaved();
        return state;
    }

    // A state as left by a previous session: a CSV load, then a JSON Object load, unsaved root
    // actions and a DrillDown session.
    private static AppState CreatePopulatedState()
    {
        var state = new AppState();
        state.StartNewFile("old.csv");
        state.CompleteCsvLoad(new AppStateTestRowIndexer("old.csv"), CreateSchema("old"));
        state.SetSchemaRefinedCallback(_ => { });
        state.SetCurrentKeyPath([new KeyPathSegment("k", KeyPathSegmentKind.Key)]);
        state.EnterJsonObjectTree([new JsonObjectEntry("id", "1"u8.ToArray())]);
        state.AddMorphAction(RootAction);
        state.EnterFocusedTable(CreateDrillDownState(new DeleteColumnAction { ColumnName = "c" }));
        return state;
    }

    // The populated state after its DrillDown session ended: a tree mode with no session.
    private static AppState CreatePopulatedTreeState()
    {
        var state = CreatePopulatedState();
        state.ExitFocusedTable(ViewMode.JsonObjectTree);
        return state;
    }

    private static (string, IReadOnlyList<KeyPathSegment>, TableSchema?, IRowIndexer?, CancellationTokenSource, Action<TableSchema>?, IReadOnlyList<MorphAction>, bool, IReadOnlyList<JsonObjectEntry>?)
        CaptureExceptMode(AppState state) =>
        (state.CurrentFilePath, state.CurrentKeyPath, state.Schema, state.RowIndexer, state.Cts,
            state.OnSchemaRefined, state.ActionStack, state.HasUnsavedChanges, state.JsonObjectEntries);

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
        var drillDown = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
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
        var drillDown = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
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
        var drillDown = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which;
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

    [Fact]
    public void HasUnsavedChanges_Default_IsFalse()
    {
        // Arrange
        using var state = new AppState();

        // Act
        var result = state.HasUnsavedChanges;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AddMorphAction_SetsUnsavedChanges()
    {
        // Arrange
        using var state = new AppState();

        // Act
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });

        // Assert
        state.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void ClearMorphActions_WhenSavedStackIsNonEmpty_SetsUnsavedChanges()
    {
        // Arrange
        using var state = new AppState();
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
        using var state = new AppState();
        var revisionBefore = state.Revision;

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
        using var state = new AppState();

        // Act
        state.SetActionStack([]);

        // Assert
        state.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void MarkRecipeSaved_AfterMutation_ClearsUnsavedChanges()
    {
        // Arrange
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });

        // Act
        state.MarkRecipeSaved();

        // Assert
        state.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void DrillDownState_WithInitialActions_DefaultsToClean()
    {
        // Arrange — the drillDownAction comes from a loaded recipe, not an unsaved edit
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        var hasUnsavedChanges = state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which.HasUnsavedChanges;

        // Assert
        hasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void MarkRecipeSavedIfUnchanged_WhenRevisionMatches_ClearsUnsavedChanges()
    {
        // Arrange
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        var savedRevision = state.Revision;

        // Act
        state.MarkRecipeSavedIfUnchanged(savedRevision);

        // Assert
        state.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void MarkRecipeSavedIfUnchanged_WhenStackMutatedSinceCapture_KeepsUnsavedChanges()
    {
        // Arrange
        using var state = new AppState();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        var savedRevision = state.Revision;
        state.ClearMorphActions();

        // Act
        state.MarkRecipeSavedIfUnchanged(savedRevision);

        // Assert
        state.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void StartNewFile_WithPath_SetsCurrentFilePath()
    {
        // Arrange
        using var state = CreatePopulatedState();

        // Act
        state.StartNewFile("new.jsonl");

        // Assert
        state.CurrentFilePath.Should().Be("new.jsonl");
    }

    [Fact]
    public void StartNewFile_AfterUnsavedEdits_EmptiesActionStackAndClearsUnsavedFlag()
    {
        // Arrange
        using var state = CreatePopulatedState();

        // Act
        state.StartNewFile("new.jsonl");

        // Assert
        state.ActionStack.Should().BeEmpty();
        state.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void StartNewFile_WhenCalled_RenewsCtsAndCancelsThePreviousOne()
    {
        // Arrange
        using var state = CreatePopulatedState();
        var previousCts = state.Cts;

        // Act
        state.StartNewFile("new.jsonl");

        // Assert
        state.Cts.Should().NotBeSameAs(previousCts);
        state.Cts.IsCancellationRequested.Should().BeFalse();
        previousCts.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void StartNewFile_WithDrillDownSessionAndJsonObjectEntries_EndsSessionAndClearsEntries()
    {
        // Arrange
        using var state = CreatePopulatedState();

        // Act
        state.StartNewFile("new.jsonl");

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.JsonObjectEntries.Should().BeNull();
    }

    [Fact]
    public void StartNewFile_WhenCalled_LeavesModeSchemaAndRowIndexerUntouched()
    {
        // Arrange
        using var state = CreatePopulatedState();
        var schemaBefore = state.Schema;
        var indexerBefore = state.RowIndexer;

        // Act
        state.StartNewFile("new.jsonl");

        // Assert
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        state.Schema.Should().BeSameAs(schemaBefore);
        state.RowIndexer.Should().BeSameAs(indexerBefore);
    }

    [Fact]
    public void BeginIndexedTreeLoad_WithIndexer_StoresIndexerAndClearsSchemaAndCallback()
    {
        // Arrange
        using var state = CreatePopulatedState();
        var indexer = new AppStateTestRowIndexer("new.jsonl");

        // Act
        state.BeginIndexedTreeLoad(indexer);

        // Assert
        state.RowIndexer.Should().BeSameAs(indexer);
        state.Schema.Should().BeNull();
        state.OnSchemaRefined.Should().BeNull();
    }

    [Fact]
    public void BeginIndexedTreeLoad_WithIndexer_LeavesModeFilePathAndActionStackUntouched()
    {
        // Arrange
        using var state = CreatePopulatedState();

        // Act
        state.BeginIndexedTreeLoad(new AppStateTestRowIndexer("new.jsonl"));

        // Assert
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        state.CurrentFilePath.Should().Be("old.csv");
        state.ActionStack.Should().Equal(RootAction);
    }

    [Fact]
    public void BeginJsonObjectLoad_AfterTableLoad_ClearsIndexerSchemaAndCallback()
    {
        // Arrange
        using var state = CreatePopulatedState();

        // Act
        state.BeginJsonObjectLoad();

        // Assert
        state.RowIndexer.Should().BeNull();
        state.Schema.Should().BeNull();
        state.OnSchemaRefined.Should().BeNull();
    }

    [Fact]
    public void BeginJsonObjectLoad_AfterTableLoad_LeavesModeFilePathActionStackAndEntriesUntouched()
    {
        // Arrange
        using var state = CreatePopulatedState();
        var entriesBefore = state.JsonObjectEntries;

        // Act
        state.BeginJsonObjectLoad();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        state.CurrentFilePath.Should().Be("old.csv");
        state.ActionStack.Should().Equal(RootAction);
        state.JsonObjectEntries.Should().BeSameAs(entriesBefore);
    }

    [Fact]
    public void CompleteCsvLoad_WithIndexerAndSchema_StoresBothAndEntersCsvTableMode()
    {
        // Arrange
        using var state = CreatePopulatedState();
        var indexer = new AppStateTestRowIndexer("new.csv");
        var schema = CreateSchema("new");

        // Act
        state.CompleteCsvLoad(indexer, schema);

        // Assert
        state.Schema.Should().BeSameAs(schema);
        state.RowIndexer.Should().BeSameAs(indexer);
        state.CurrentMode.Should().Be(ViewMode.CsvTable);
    }

    [Fact]
    public void CompleteCsvLoad_WithIndexerAndSchema_LeavesFilePathAndActionStackUntouched()
    {
        // Arrange
        using var state = CreatePopulatedState();

        // Act
        state.CompleteCsvLoad(new AppStateTestRowIndexer("new.csv"), CreateSchema("new"));

        // Assert
        state.CurrentFilePath.Should().Be("old.csv");
        state.ActionStack.Should().Equal(RootAction);
    }

    [Fact]
    public void EnterJsonObjectTree_WithEntries_StoresEntriesAndEntersJsonObjectTreeMode()
    {
        // Arrange
        using var state = new AppState();
        IReadOnlyList<JsonObjectEntry> entries = [new JsonObjectEntry("orders", "[]"u8.ToArray())];

        // Act
        state.EnterJsonObjectTree(entries);

        // Assert
        state.JsonObjectEntries.Should().BeSameAs(entries);
        state.CurrentMode.Should().Be(ViewMode.JsonObjectTree);
    }

    [Fact]
    public void EnterJsonObjectTree_WithEntries_LeavesSchemaAndRowIndexerUntouched()
    {
        // Arrange
        using var state = new AppState();
        var indexer = new AppStateTestRowIndexer("data.csv");
        var schema = CreateSchema();
        state.CompleteCsvLoad(indexer, schema);

        // Act
        state.EnterJsonObjectTree([new JsonObjectEntry("orders", "[]"u8.ToArray())]);

        // Assert
        state.Schema.Should().BeSameAs(schema);
        state.RowIndexer.Should().BeSameAs(indexer);
    }

    [Fact]
    public void EnterJsonLinesTree_FromOtherMode_ChangesOnlyTheMode()
    {
        // Arrange
        using var state = CreatePopulatedTreeState();
        var before = CaptureExceptMode(state);

        // Act
        state.EnterJsonLinesTree();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
        CaptureExceptMode(state).Should().Be(before);
    }

    [Fact]
    public void EnterJsonArrayTree_FromOtherMode_ChangesOnlyTheMode()
    {
        // Arrange
        using var state = CreatePopulatedTreeState();
        var before = CaptureExceptMode(state);

        // Act
        state.EnterJsonArrayTree();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonArrayTree);
        CaptureExceptMode(state).Should().Be(before);
    }

    [Fact]
    public void EnterJsonLinesTable_FromOtherMode_ChangesOnlyTheMode()
    {
        // Arrange
        using var state = CreatePopulatedTreeState();
        var before = CaptureExceptMode(state);

        // Act
        state.EnterJsonLinesTable();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
        CaptureExceptMode(state).Should().Be(before);
    }

    [Fact]
    public void CompleteJsonLinesSchemaScan_WithSchema_StoresSchemaAndEntersJsonLinesTableMode()
    {
        // Arrange
        using var state = CreatePopulatedState();
        var schema = CreateSchema("scanned");

        // Act
        state.CompleteJsonLinesSchemaScan(schema);

        // Assert
        state.Schema.Should().BeSameAs(schema);
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
    }

    [Fact]
    public void CompleteJsonLinesSchemaScan_WithSchema_LeavesRowIndexerFilePathAndActionStackUntouched()
    {
        // Arrange
        using var state = CreatePopulatedState();
        var indexerBefore = state.RowIndexer;

        // Act
        state.CompleteJsonLinesSchemaScan(CreateSchema("scanned"));

        // Assert
        state.RowIndexer.Should().BeSameAs(indexerBefore);
        state.CurrentFilePath.Should().Be("old.csv");
        state.ActionStack.Should().Equal(RootAction);
    }

    [Fact]
    public void EnterPlaceholderMode_FromOtherMode_ChangesOnlyTheMode()
    {
        // Arrange
        using var state = CreatePopulatedTreeState();
        var before = CaptureExceptMode(state);

        // Act
        state.EnterPlaceholderMode();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        CaptureExceptMode(state).Should().Be(before);
    }

    [Fact]
    public void EnterPlaceholderMode_WithDrillDownSession_EndsTheSession()
    {
        // Arrange
        using var state = CreatePopulatedState();

        // Act
        state.EnterPlaceholderMode();

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
    }

    [Fact]
    public void CompleteCsvLoad_WithDrillDownSession_EndsTheSession()
    {
        // Arrange
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        state.CompleteCsvLoad(new AppStateTestRowIndexer("data.csv"), CreateSchema());

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.CsvTable);
    }

    [Fact]
    public void EnterJsonObjectTree_WithDrillDownSession_EndsTheSession()
    {
        // Arrange
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        state.EnterJsonObjectTree([new JsonObjectEntry("orders", "[]"u8.ToArray())]);

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.JsonObjectTree);
    }

    [Fact]
    public void EnterJsonLinesTree_WithDrillDownSession_EndsTheSession()
    {
        // Arrange
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        state.EnterJsonLinesTree();

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Fact]
    public void EnterJsonArrayTree_WithDrillDownSession_EndsTheSession()
    {
        // Arrange
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        state.EnterJsonArrayTree();

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.JsonArrayTree);
    }

    [Fact]
    public void EnterJsonLinesTable_WithDrillDownSession_EndsTheSession()
    {
        // Arrange
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        state.EnterJsonLinesTable();

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
    }

    [Fact]
    public void CompleteJsonLinesSchemaScan_WithDrillDownSession_EndsTheSession()
    {
        // Arrange
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        state.CompleteJsonLinesSchemaScan(CreateSchema());

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
    }

    [Fact]
    public void EnterFocusedTable_WithSession_EntersFocusedTableModeWithThatSession()
    {
        // Arrange
        using var state = new AppState();
        var drillDown = CreateDrillDownState(new DeleteColumnAction { ColumnName = "c" });

        // Act
        state.EnterFocusedTable(drillDown);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        state.TryGetDrillDown(out var session).Should().BeTrue();
        session.Should().BeSameAs(drillDown);
    }

    [Fact]
    public void EnterFocusedTable_WithSession_LeavesSchemaRowIndexerAndActionStackUntouched()
    {
        // Arrange
        using var state = new AppState();
        var indexer = new AppStateTestRowIndexer("data.csv");
        var schema = CreateSchema();
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
    public void ApplyRefinedSchema_WithSchema_ReplacesSchemaAndLeavesModeAndRowIndexerUntouched()
    {
        // Arrange
        using var state = CreatePopulatedState();
        var indexerBefore = state.RowIndexer;
        var refined = CreateSchema("refined");

        // Act
        state.ApplyRefinedSchema(refined);

        // Assert
        state.Schema.Should().BeSameAs(refined);
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        state.RowIndexer.Should().BeSameAs(indexerBefore);
    }

    [Fact]
    public void SetSchemaRefinedCallback_WithCallback_StoresCallback()
    {
        // Arrange
        using var state = new AppState();
        Action<TableSchema> callback = _ => { };

        // Act
        state.SetSchemaRefinedCallback(callback);

        // Assert
        state.OnSchemaRefined.Should().BeSameAs(callback);
    }

    [Fact]
    public void SetSchemaRefinedCallback_WithNull_ClearsCallbackAndLeavesSchemaUntouched()
    {
        // Arrange
        using var state = CreatePopulatedState();
        var schemaBefore = state.Schema;

        // Act
        state.SetSchemaRefinedCallback(null);

        // Assert
        state.OnSchemaRefined.Should().BeNull();
        state.Schema.Should().BeSameAs(schemaBefore);
    }

    [Fact]
    public void SetCurrentKeyPath_WithPath_StoresPath()
    {
        // Arrange
        using var state = new AppState();
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
        using var state = CreatePopulatedState();

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
        var drillDownAction = new RenameColumnAction { OldName = "x", NewName = "y" };
        using var state = CreateStateWithDrillDown(drillDownAction);

        // Act
        var found = state.TryGetDrillDown(out var drillDown);

        // Assert
        found.Should().BeTrue();
        var actual = drillDown.Should().BeOfType<DrillDownState>().Which;
        actual.ActionStack.Should().Equal(drillDownAction);
    }

    [Fact]
    public void TryGetDrillDown_WhenNoSessionStarted_ReturnsFalse()
    {
        // Arrange
        using var state = new AppState();

        // Act
        var found = state.TryGetDrillDown(out var drillDown);

        // Assert
        found.Should().BeFalse();
        drillDown.Should().BeNull();
    }

    [Fact]
    public void TryGetDrillDown_AfterPlaceholderModeReplacedFocusedTable_ReturnsFalse()
    {
        // Arrange
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });
        state.EnterPlaceholderMode();

        // Act
        var found = state.TryGetDrillDown(out var drillDown);

        // Assert
        found.Should().BeFalse();
        drillDown.Should().BeNull();
    }

    [Fact]
    public void ExitFocusedTable_WithSession_RestoresGivenModeAndEndsSession()
    {
        // Arrange
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

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
        var existing = new RenameColumnAction { OldName = "x", NewName = "y" };
        var added = new DeleteColumnAction { ColumnName = "c" };
        using var state = CreateStateWithDrillDown(existing);

        // Act
        state.AddDrillDownAction(added);

        // Assert
        state.TryGetDrillDown(out var drillDown).Should().BeTrue();
        var actual = drillDown.Should().BeOfType<DrillDownState>().Which;
        actual.ActionStack.Should().Equal(existing, added);
        actual.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void AddDrillDownAction_WithSession_LeavesRootActionStackUnsavedFlagAndModeUntouched()
    {
        // Arrange
        using var state = CreateSavedRootState(CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" }));

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
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });
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
        using var state = CreateSavedRootState(new AppState());
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
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        state.ClearDrillDownActions();

        // Assert
        state.TryGetDrillDown(out var drillDown).Should().BeTrue();
        var actual = drillDown.Should().BeOfType<DrillDownState>().Which;
        actual.ActionStack.Should().BeEmpty();
        actual.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void ClearDrillDownActions_WithSession_LeavesRootActionStackUnsavedFlagAndModeUntouched()
    {
        // Arrange
        using var state = CreateSavedRootState(CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" }));

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
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });
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
        using var state = CreateSavedRootState(new AppState());
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
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });
        state.AddDrillDownAction(new DeleteColumnAction { ColumnName = "c" });
        state.TryGetDrillDown(out var before).Should().BeTrue();
        var actualBefore = before.Should().BeOfType<DrillDownState>().Which;

        // Act
        state.MarkDrillDownSaved();

        // Assert
        state.TryGetDrillDown(out var after).Should().BeTrue();
        var actualAfter = after.Should().BeOfType<DrillDownState>().Which;
        actualAfter.HasUnsavedChanges.Should().BeFalse();
        actualAfter.ActionStack.Should().Equal(actualBefore.ActionStack);
    }

    [Fact]
    public void MarkDrillDownSaved_WithSession_LeavesRootActionStackUnsavedFlagAndModeUntouched()
    {
        // Arrange
        using var state = CreateSavedRootState(CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" }));
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
        using var state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });
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
        using var state = CreateSavedRootState(new AppState());
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

    private sealed class AppStateTestRowIndexer(string filePath) : IRowIndexer
    {
        public string FilePath => filePath;
        public long FileSize => 1000;
        public long BytesRead => 1000;
        public long TotalRows => 10;
        public bool IsIndexingCompleted => true;

#pragma warning disable CS0067
        public event Action? FirstCheckpointReached;
        public event Action<long, long>? ProgressChanged;
        public event Action? BuildIndexCompleted;
#pragma warning restore CS0067

        public void BuildIndex(CancellationToken cancellationToken = default) { }

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => (0, 0);
    }
}

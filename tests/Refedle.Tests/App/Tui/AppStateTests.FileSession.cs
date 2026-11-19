using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.Engine.IO;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.App.Tui;

public sealed partial class AppStateTests
{
    [Fact]
    public void JsonObjectEntries_Default_IsNull()
    {
        // Arrange
        using AppState state = new();

        // Act
        IReadOnlyList<JsonObjectEntry>? result = state.JsonObjectEntries;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void StartNewFile_WithPath_SetsCurrentFilePath()
    {
        // Arrange
        using AppState state = CreatePopulatedState();

        // Act
        state.StartNewFile("new.jsonl");

        // Assert
        state.CurrentFilePath.Should().Be("new.jsonl");
    }

    [Fact]
    public void StartNewFile_AfterUnsavedEdits_EmptiesActionStackAndClearsUnsavedFlag()
    {
        // Arrange
        using AppState state = CreatePopulatedState();

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
        using AppState state = CreatePopulatedState();
        CancellationTokenSource previousCts = state.Cts;

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
        using AppState state = CreatePopulatedState();

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
        using AppState state = CreatePopulatedState();
        TableSchema? schemaBefore = state.Schema;
        IRowIndexer? indexerBefore = state.RowIndexer;

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
        using AppState state = CreatePopulatedState();
        AppStateTestRowIndexer indexer = new("new.jsonl");

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
        using AppState state = CreatePopulatedState();

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
        using AppState state = CreatePopulatedState();

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
        using AppState state = CreatePopulatedState();
        IReadOnlyList<JsonObjectEntry>? entriesBefore = state.JsonObjectEntries;

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
        using AppState state = CreatePopulatedState();
        AppStateTestRowIndexer indexer = new("new.csv");
        TableSchema schema = CreateSchema("new");

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
        using AppState state = CreatePopulatedState();

        // Act
        state.CompleteCsvLoad(new AppStateTestRowIndexer("new.csv"), CreateSchema("new"));

        // Assert
        state.CurrentFilePath.Should().Be("old.csv");
        state.ActionStack.Should().Equal(RootAction);
    }

    [Fact]
    public void CompleteJsonLinesSchemaScan_WithSchema_StoresSchemaAndEntersJsonLinesTableMode()
    {
        // Arrange
        using AppState state = CreatePopulatedState();
        TableSchema schema = CreateSchema("scanned");

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
        using AppState state = CreatePopulatedState();
        IRowIndexer? indexerBefore = state.RowIndexer;

        // Act
        state.CompleteJsonLinesSchemaScan(CreateSchema("scanned"));

        // Assert
        state.RowIndexer.Should().BeSameAs(indexerBefore);
        state.CurrentFilePath.Should().Be("old.csv");
        state.ActionStack.Should().Equal(RootAction);
    }

    [Fact]
    public void CompleteCsvLoad_WithDrillDownSession_EndsTheSession()
    {
        // Arrange
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        state.CompleteCsvLoad(new AppStateTestRowIndexer("data.csv"), CreateSchema());

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.CsvTable);
    }

    [Fact]
    public void CompleteJsonLinesSchemaScan_WithDrillDownSession_EndsTheSession()
    {
        // Arrange
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        state.CompleteJsonLinesSchemaScan(CreateSchema());

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
    }

    [Fact]
    public void ApplyRefinedSchema_WithSchema_ReplacesSchemaAndLeavesModeAndRowIndexerUntouched()
    {
        // Arrange
        using AppState state = CreatePopulatedState();
        IRowIndexer? indexerBefore = state.RowIndexer;
        TableSchema refined = CreateSchema("refined");

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
        using AppState state = new();
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
        using AppState state = CreatePopulatedState();
        TableSchema? schemaBefore = state.Schema;

        // Act
        state.SetSchemaRefinedCallback(null);

        // Assert
        state.OnSchemaRefined.Should().BeNull();
        state.Schema.Should().BeSameAs(schemaBefore);
    }
}

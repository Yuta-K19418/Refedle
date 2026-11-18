using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.App.Tui;

public sealed partial class AppStateTests
{
    [Fact]
    public void EnterJsonObjectTree_WithEntries_StoresEntriesAndEntersJsonObjectTreeMode()
    {
        // Arrange
        using AppState state = new();
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
        using AppState state = new();
        AppStateTestRowIndexer indexer = new("data.csv");
        TableSchema schema = CreateSchema();
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
        using AppState state = CreatePopulatedTreeState();
        (string, IReadOnlyList<KeyPathSegment>, TableSchema?, IRowIndexer?, CancellationTokenSource, Action<TableSchema>?, IReadOnlyList<IMorphAction>, bool, IReadOnlyList<JsonObjectEntry>?) before = CaptureExceptMode(state);

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
        using AppState state = CreatePopulatedTreeState();
        (string, IReadOnlyList<KeyPathSegment>, TableSchema?, IRowIndexer?, CancellationTokenSource, Action<TableSchema>?, IReadOnlyList<IMorphAction>, bool, IReadOnlyList<JsonObjectEntry>?) before = CaptureExceptMode(state);

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
        using AppState state = CreatePopulatedTreeState();
        (string, IReadOnlyList<KeyPathSegment>, TableSchema?, IRowIndexer?, CancellationTokenSource, Action<TableSchema>?, IReadOnlyList<IMorphAction>, bool, IReadOnlyList<JsonObjectEntry>?) before = CaptureExceptMode(state);

        // Act
        state.EnterJsonLinesTable();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
        CaptureExceptMode(state).Should().Be(before);
    }

    [Fact]
    public void EnterPlaceholderMode_FromOtherMode_ChangesOnlyTheMode()
    {
        // Arrange
        using AppState state = CreatePopulatedTreeState();
        (string, IReadOnlyList<KeyPathSegment>, TableSchema?, IRowIndexer?, CancellationTokenSource, Action<TableSchema>?, IReadOnlyList<IMorphAction>, bool, IReadOnlyList<JsonObjectEntry>?) before = CaptureExceptMode(state);

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
        using AppState state = CreatePopulatedState();

        // Act
        state.EnterPlaceholderMode();

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
    }

    [Fact]
    public void EnterJsonObjectTree_WithDrillDownSession_EndsTheSession()
    {
        // Arrange
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

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
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

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
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

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
        using AppState state = CreateStateWithDrillDown(new RenameColumnAction { OldName = "x", NewName = "y" });

        // Act
        state.EnterJsonLinesTable();

        // Assert
        state.TryGetDrillDown(out _).Should().BeFalse();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
    }
}

using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Recipes;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui;
using Refedle.TestHelpers.App.Tui.Workers.Schema;
using Refedle.TestHelpers.IO;
using Terminal.Gui.ViewBase;

namespace Refedle.IntegrationTests.App.Tui.UI;

public sealed partial class RecipeCommandHandlerTests : IDisposable
{
    private readonly TempFileCollection _tempFiles = new();

    private readonly string _jsonObjectFile;

    private readonly string _jsonLinesFile;

    private readonly string _csvFile;

    private readonly string _recipeFile;

    public RecipeCommandHandlerTests()
    {
        _jsonObjectFile = _tempFiles.ReservePath(".json");
        _jsonLinesFile = _tempFiles.ReservePath(".jsonl");
        _csvFile = _tempFiles.ReservePath(".csv");
        _recipeFile = _tempFiles.ReservePath(".yaml");
    }

    public void Dispose() => _tempFiles.Dispose();

    private static async Task SaveRecipeAsync(Recipe recipe, string path)
    {
        Result saveResult = await new RecipeManager().SaveAsync(recipe, path);
        saveResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LoadFromPathAsync_NonDrillDownRecipe_SetsBaseActionStack()
    {
        // Arrange
        RenameColumnAction action = new() { OldName = "old", NewName = "new" };
        await SaveRecipeAsync(new Recipe { Name = "test", Actions = [action] }, _recipeFile);

        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            state.StartNewFile(_jsonLinesFile);
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        IMorphAction[] actionStack = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.State.ActionStack.ToArray()));

        // Assert
        actionStack.Should().Equal(action);
    }

    [Fact]
    public async Task LoadFromPathAsync_NonDrillDownRecipe_MarksRootStackSaved()
    {
        // Arrange — the session was dirty before the load; loading mirrors the recipe file, so the
        // loaded stack must not count as unsaved changes
        RenameColumnAction action = new() { OldName = "old", NewName = "new" };
        await SaveRecipeAsync(new Recipe { Name = "test", Actions = [action] }, _recipeFile);

        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            state.StartNewFile(_jsonLinesFile);
            state.AddMorphAction(new DeleteColumnAction { ColumnName = "stale" });
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        bool hasUnsavedChanges = await session.InvokeAsync(
            (_, ctx) => Task.FromResult(ctx.State.HasUnsavedChanges));

        // Assert
        hasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public async Task LoadFromPathAsync_DrillDownRecipe_DrillDownSessionStartsClean()
    {
        // Arrange — a replayed DrillDown recipe re-creates the session from the recipe's actions,
        // so the fresh session must start without unsaved changes
        File.WriteAllText(_jsonLinesFile, "{\"user\":{\"name\":\"Alice\"}}\n{\"user\":{\"name\":\"Bob\"}}");

        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("user", KeyPathSegmentKind.Key)];
        RenameColumnAction action = new() { OldName = "name", NewName = "fullName" };
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [action], DrillDownKeyPath = keyPath }, _recipeFile);

        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            state.StartNewFile(_jsonLinesFile);
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        bool? drillDownFlag = await session.InvokeAsync(
            (_, ctx) => Task.FromResult(ctx.State.GetDrillDownOrNull()?.HasUnsavedChanges));

        // Assert
        drillDownFlag.Should().BeFalse();
    }

    [Fact]
    public async Task LoadFromPathAsync_JsonObjectRecipeWithMatchingEntry_RendersFocusedTableWithRecipeActionApplied()
    {
        // Arrange
        File.WriteAllText(_jsonObjectFile, """{"orders":[{"id":"A1"},{"id":"A2"}]}""");

        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("orders", KeyPathSegmentKind.Key)];
        RenameColumnAction action = new() { OldName = "id", NewName = "orderId" };
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [action], DrillDownKeyPath = keyPath }, _recipeFile);

        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            state.StartNewFile(_jsonObjectFile);
            state.EnterJsonObjectTree([new Refedle.Engine.IO.JsonObject.JsonObjectEntry(
                "orders", """[{"id":"A1"},{"id":"A2"}]"""u8.ToArray())]);
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        (ViewMode mode, KeyPathSegment[]? drillDownKeyPath, IMorphAction[]? drillDownActionStack, bool isFocusedTableTransformer, string[]? columnNames) =
            await session.InvokeAsync((_, ctx) =>
            {
                View? view = ctx.ViewManager.GetCurrentView();
                bool isTransformer = view is FocusedTableView
                {
                    Table: PagedTableSource { Inner: ColumnWidthStabilizingTableSource { Inner: FocusedTableTransformer } }
                };
                string[]? columns = view is FocusedTableView
                {
                    Table: PagedTableSource { Inner: ColumnWidthStabilizingTableSource { Inner: FocusedTableTransformer } decoratedTable }
                }
                    ? [.. decoratedTable.ColumnNames]
                    : null;
                DrillDownState? drillDown = ctx.State.GetDrillDownOrNull();
                return Task.FromResult((
                    ctx.State.CurrentMode,
                    drillDown?.KeyPath.ToArray(),
                    drillDown?.ActionStack.ToArray(),
                    isTransformer,
                    columns));
            });

        // Assert
        mode.Should().Be(ViewMode.FocusedTable);
        drillDownKeyPath.Should().Equal(keyPath);
        drillDownActionStack.Should().Equal(action);

        isFocusedTableTransformer.Should().BeTrue();
        columnNames.Should().ContainSingle(name => name.StartsWith("orderId", StringComparison.Ordinal));
        columnNames.Should().NotContain(name => name.StartsWith("id ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadFromPathAsync_JsonLinesRecipe_RendersFocusedTableWithRecipeActionApplied()
    {
        // Arrange
        File.WriteAllText(_jsonLinesFile, "{\"user\":{\"name\":\"Alice\"}}\n{\"user\":{\"name\":\"Bob\"}}");

        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("user", KeyPathSegmentKind.Key)];
        RenameColumnAction action = new() { OldName = "name", NewName = "fullName" };
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [action], DrillDownKeyPath = keyPath }, _recipeFile);

        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            state.StartNewFile(_jsonLinesFile);
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        (ViewMode mode, KeyPathSegment[]? drillDownKeyPath, IMorphAction[]? drillDownActionStack, bool isFocusedTableTransformer, string[]? columnNames) =
            await session.InvokeAsync((_, ctx) =>
            {
                View? view = ctx.ViewManager.GetCurrentView();
                bool isTransformer = view is FocusedTableView
                {
                    Table: PagedTableSource { Inner: ColumnWidthStabilizingTableSource { Inner: FocusedTableTransformer } }
                };
                string[]? columns = view is FocusedTableView
                {
                    Table: PagedTableSource { Inner: ColumnWidthStabilizingTableSource { Inner: FocusedTableTransformer } decoratedTable }
                }
                    ? [.. decoratedTable.ColumnNames]
                    : null;
                DrillDownState? drillDown = ctx.State.GetDrillDownOrNull();
                return Task.FromResult((
                    ctx.State.CurrentMode,
                    drillDown?.KeyPath.ToArray(),
                    drillDown?.ActionStack.ToArray(),
                    isTransformer,
                    columns));
            });

        // Assert
        mode.Should().Be(ViewMode.FocusedTable);
        drillDownKeyPath.Should().Equal(keyPath);
        drillDownActionStack.Should().Equal(action);

        isFocusedTableTransformer.Should().BeTrue();
        columnNames.Should().ContainSingle(name => name.StartsWith("fullName", StringComparison.Ordinal));
        columnNames.Should().NotContain(name => name.StartsWith("name ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadFromPathAsync_JsonObjectRecipeWithMissingFirstSegment_ShowsErrorInsteadOfLoadingBaseTable()
    {
        // Arrange — the recorded first segment no longer matches this file's top-level entries
        // (e.g. the file changed since the recipe was saved).
        File.WriteAllText(_jsonObjectFile, """{"orders":[{"id":"A1"}]}""");

        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("missing", KeyPathSegmentKind.Key)];
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [], DrillDownKeyPath = keyPath }, _recipeFile);

        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            state.StartNewFile(_jsonObjectFile);
            state.EnterJsonObjectTree([new Refedle.Engine.IO.JsonObject.JsonObjectEntry(
                "orders", """[{"id":"A1"}]"""u8.ToArray())]);
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        (ViewMode mode, bool isPlaceholder, string? placeholderText) = await session.InvokeAsync((_, ctx) =>
        {
            View? view = ctx.ViewManager.GetCurrentView();
            string? text = view is PlaceholderView placeholderView ? placeholderView.Text : null;
            return Task.FromResult((ctx.State.CurrentMode, view is PlaceholderView, text));
        });

        // Assert
        mode.Should().Be(ViewMode.PlaceholderView);
        isPlaceholder.Should().BeTrue();
        placeholderText.Should().Contain("not found");
    }

    [Fact]
    public async Task LoadFromPathAsync_JsonObjectRecipeWithEmptyDrillDownKeyPath_ShowsDistinctError()
    {
        // Arrange — an empty DrillDownKeyPath against a JSON Object file: e.g. hand-edited YAML,
        // or a Full Aggregation DrillDown recipe mistakenly loaded against a JSON Object file.
        File.WriteAllText(_jsonObjectFile, """{"orders":[{"id":"A1"}]}""");

        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [], DrillDownKeyPath = [] }, _recipeFile);

        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            state.StartNewFile(_jsonObjectFile);
            state.EnterJsonObjectTree([new Refedle.Engine.IO.JsonObject.JsonObjectEntry(
                "orders", """[{"id":"A1"}]"""u8.ToArray())]);
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        (ViewMode mode, bool isPlaceholder, string? placeholderText) = await session.InvokeAsync((_, ctx) =>
        {
            View? view = ctx.ViewManager.GetCurrentView();
            string? text = view is PlaceholderView placeholderView ? placeholderView.Text : null;
            return Task.FromResult((ctx.State.CurrentMode, view is PlaceholderView, text));
        });

        // Assert
        mode.Should().Be(ViewMode.PlaceholderView);
        isPlaceholder.Should().BeTrue();
        placeholderText.Should().Contain("empty");
    }

    [Fact]
    public async Task LoadFromPathAsync_DrillDownRecipeAgainstCsvFile_ShowsErrorInsteadOfCrashing()
    {
        // Arrange — CSV is not a Full Aggregation DrillDown format (only JSON Lines/Array are),
        // so this must surface an explicit error rather than reach FullAggregationScanner.Scan,
        // which throws UnreachableException for any other format.
        File.WriteAllText(_csvFile, "id,name\n1,Alice\n");

        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("user", KeyPathSegmentKind.Key)];
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [], DrillDownKeyPath = keyPath }, _recipeFile);

        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            state.StartNewFile(_csvFile);
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        Func<Task> act = async () =>
            await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());

        // Assert
        await act.Should().NotThrowAsync();
        (bool isPlaceholder, string? placeholderText) = await session.InvokeAsync((_, ctx) =>
        {
            View? view = ctx.ViewManager.GetCurrentView();
            string? text = view is PlaceholderView placeholderView ? placeholderView.Text : null;
            return Task.FromResult((view is PlaceholderView, text));
        });
        isPlaceholder.Should().BeTrue();
        placeholderText.Should().Contain("Csv");
    }

    [Fact]
    public async Task LoadFromPathAsync_JsonLinesRecipeWithUnmatchedKeyPath_EndsExistingDrillDownSessionOnFailure()
    {
        // Arrange — the scan finds no matching rows, so FullAggregationDrillDownAsync fails and the
        // error view replaces FocusedTable, which ends the pre-existing DrillDown session.
        File.WriteAllText(_jsonLinesFile, "{\"user\":{\"name\":\"Alice\"}}\n{\"user\":{\"name\":\"Bob\"}}");

        TableSchema existingSchema = new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        RenameColumnAction existingAction = new() { OldName = "existing", NewName = "renamed_existing" };
        DrillDownState existingDrillDown = new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            existingSchema,
            ViewMode.JsonLinesTree,
            KeyPath: [new KeyPathSegment("previous", KeyPathSegmentKind.Key)],
            ActionStack: [existingAction]);

        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("missing", KeyPathSegmentKind.Key)];
        RenameColumnAction recipeAction = new() { OldName = "name", NewName = "fullName" };
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [recipeAction], DrillDownKeyPath = keyPath }, _recipeFile);

        await using LivePumpTestSession<LiveTestContext<RecipeCommandHandler>> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            AppState state = new();
            state.StartNewFile(_jsonLinesFile);
            state.EnterFocusedTable(existingDrillDown);
            ModeController modeController = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager viewManager = new(window, state, modeController, new FilterIndexingRunner(), app.Invoke);
            RecipeCommandHandler handler = new(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        DrillDownState? drillDown = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.State.GetDrillDownOrNull()));
        ViewMode mode = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.State.CurrentMode));

        // Assert
        drillDown.Should().BeNull();
        mode.Should().Be(ViewMode.PlaceholderView);
    }
}

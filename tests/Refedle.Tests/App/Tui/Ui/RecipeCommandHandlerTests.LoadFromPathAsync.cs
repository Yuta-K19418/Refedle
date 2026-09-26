using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.Ui;
using Refedle.App.Tui.Ui.Views;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Recipes;
using Refedle.Engine.Types;
using Refedle.Tests.App.Tui.Workers.Schema;

namespace Refedle.Tests.App.Tui.Ui;

public sealed partial class RecipeCommandHandlerTests : IDisposable
{
    private readonly string _jsonObjectFile;
    private readonly string _jsonLinesFile;
    private readonly string _csvFile;
    private readonly string _recipeFile;

    public RecipeCommandHandlerTests()
    {
        _jsonObjectFile = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        _jsonLinesFile = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
        _csvFile = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        _recipeFile = Path.ChangeExtension(Path.GetTempFileName(), ".yaml");
    }

    public void Dispose()
    {
        if (File.Exists(_jsonObjectFile))
        {
            File.Delete(_jsonObjectFile);
        }

        if (File.Exists(_jsonLinesFile))
        {
            File.Delete(_jsonLinesFile);
        }

        if (File.Exists(_csvFile))
        {
            File.Delete(_csvFile);
        }

        if (File.Exists(_recipeFile))
        {
            File.Delete(_recipeFile);
        }
    }

    private static async Task SaveRecipeAsync(Recipe recipe, string path)
    {
        var saveResult = await new RecipeManager().SaveAsync(recipe, path);
        saveResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LoadFromPathAsync_NonDrillDownRecipe_SetsBaseActionStack()
    {
        // Arrange
        var action = new RenameColumnAction { OldName = "old", NewName = "new" };
        await SaveRecipeAsync(new Recipe { Name = "test", Actions = [action] }, _recipeFile);

        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            var state = new AppState { CurrentFilePath = _jsonLinesFile };
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        var actionStack = await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.State.ActionStack.ToArray()));

        // Assert
        actionStack.Should().Equal(action);
    }

    [Fact]
    public async Task LoadFromPathAsync_NonDrillDownRecipe_MarksRootStackSaved()
    {
        // Arrange — the session was dirty before the load; loading mirrors the recipe file, so the
        // loaded stack must not count as unsaved changes
        var action = new RenameColumnAction { OldName = "old", NewName = "new" };
        await SaveRecipeAsync(new Recipe { Name = "test", Actions = [action] }, _recipeFile);

        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            var state = new AppState { CurrentFilePath = _jsonLinesFile };
            state.AddMorphAction(new DeleteColumnAction { ColumnName = "stale" });
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        var hasUnsavedChanges = await session.InvokeAsync(
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
        var action = new RenameColumnAction { OldName = "name", NewName = "fullName" };
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [action], DrillDownKeyPath = keyPath }, _recipeFile);

        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            var state = new AppState { CurrentFilePath = _jsonLinesFile };
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        var drillDownFlag = await session.InvokeAsync(
            (_, ctx) => Task.FromResult(ctx.State.DrillDown?.HasUnsavedChanges));

        // Assert
        drillDownFlag.Should().BeFalse();
    }

    [Fact]
    public async Task LoadFromPathAsync_JsonObjectRecipeWithMatchingEntry_RendersFocusedTableWithRecipeActionApplied()
    {
        // Arrange
        File.WriteAllText(_jsonObjectFile, """{"orders":[{"id":"A1"},{"id":"A2"}]}""");

        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("orders", KeyPathSegmentKind.Key)];
        var action = new RenameColumnAction { OldName = "id", NewName = "orderId" };
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [action], DrillDownKeyPath = keyPath }, _recipeFile);

        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            var state = new AppState
            {
                CurrentFilePath = _jsonObjectFile,
                JsonObjectEntries = [new Refedle.Engine.IO.JsonObject.JsonObjectEntry(
                    "orders", """[{"id":"A1"},{"id":"A2"}]"""u8.ToArray())],
            };
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        var (mode, drillDownKeyPath, drillDownActionStack, isFocusedTableTransformer, columnNames) =
            await session.InvokeAsync((_, ctx) =>
            {
                var view = ctx.ViewManager.GetCurrentView();
                var isTransformer = view is FocusedTableView { Table: ColumnWidthStabilizingTableSource { Inner: FocusedTableTransformer } };
                var columns = view is FocusedTableView { Table: ColumnWidthStabilizingTableSource { Inner: FocusedTableTransformer } decoratedTable }
                    ? decoratedTable.ColumnNames.ToArray()
                    : null;
                var drillDown = ctx.State.DrillDown;
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
        var action = new RenameColumnAction { OldName = "name", NewName = "fullName" };
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [action], DrillDownKeyPath = keyPath }, _recipeFile);

        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            var state = new AppState { CurrentFilePath = _jsonLinesFile };
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        var (mode, drillDownKeyPath, drillDownActionStack, isFocusedTableTransformer, columnNames) =
            await session.InvokeAsync((_, ctx) =>
            {
                var view = ctx.ViewManager.GetCurrentView();
                var isTransformer = view is FocusedTableView { Table: ColumnWidthStabilizingTableSource { Inner: FocusedTableTransformer } };
                var columns = view is FocusedTableView { Table: ColumnWidthStabilizingTableSource { Inner: FocusedTableTransformer } decoratedTable }
                    ? decoratedTable.ColumnNames.ToArray()
                    : null;
                var drillDown = ctx.State.DrillDown;
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

        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            var state = new AppState
            {
                CurrentFilePath = _jsonObjectFile,
                JsonObjectEntries = [new Refedle.Engine.IO.JsonObject.JsonObjectEntry(
                    "orders", """[{"id":"A1"}]"""u8.ToArray())],
            };
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        var (mode, isPlaceholder, placeholderText) = await session.InvokeAsync((_, ctx) =>
        {
            var view = ctx.ViewManager.GetCurrentView();
            var text = view is PlaceholderView placeholderView ? placeholderView.Text : null;
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

        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            var state = new AppState
            {
                CurrentFilePath = _jsonObjectFile,
                JsonObjectEntries = [new Refedle.Engine.IO.JsonObject.JsonObjectEntry(
                    "orders", """[{"id":"A1"}]"""u8.ToArray())],
            };
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        var (mode, isPlaceholder, placeholderText) = await session.InvokeAsync((_, ctx) =>
        {
            var view = ctx.ViewManager.GetCurrentView();
            var text = view is PlaceholderView placeholderView ? placeholderView.Text : null;
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

        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            var state = new AppState { CurrentFilePath = _csvFile };
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        Func<Task> act = async () =>
            await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());

        // Assert
        await act.Should().NotThrowAsync();
        var (isPlaceholder, placeholderText) = await session.InvokeAsync((_, ctx) =>
        {
            var view = ctx.ViewManager.GetCurrentView();
            var text = view is PlaceholderView placeholderView ? placeholderView.Text : null;
            return Task.FromResult((view is PlaceholderView, text));
        });
        isPlaceholder.Should().BeTrue();
        placeholderText.Should().Contain("Csv");
    }

    [Fact]
    public async Task LoadFromPathAsync_JsonLinesRecipeWithUnmatchedKeyPath_PreservesExistingDrillDownActionStackOnFailure()
    {
        // Arrange — the scan finds no matching rows, so FullAggregationDrillDownAsync fails; the
        // pre-existing DrillDown session (and its ActionStack) must be left untouched rather than
        // overwritten before the transition is known to succeed.
        File.WriteAllText(_jsonLinesFile, "{\"user\":{\"name\":\"Alice\"}}\n{\"user\":{\"name\":\"Bob\"}}");

        var existingSchema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        var existingAction = new RenameColumnAction { OldName = "existing", NewName = "renamed_existing" };
        var existingDrillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            existingSchema,
            ViewMode.JsonLinesTree,
            KeyPath: [new KeyPathSegment("previous", KeyPathSegmentKind.Key)],
            ActionStack: [existingAction]);

        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("missing", KeyPathSegmentKind.Key)];
        var recipeAction = new RenameColumnAction { OldName = "name", NewName = "fullName" };
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [recipeAction], DrillDownKeyPath = keyPath }, _recipeFile);

        await using var session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            var state = new AppState
            {
                CurrentFilePath = _jsonLinesFile,
                CurrentMode = ViewMode.FocusedTable,
                DrillDown = existingDrillDown,
            };
            var modeController = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
            var viewManager = new ViewManager(window, state, modeController, app.Invoke);
            var handler = new RecipeCommandHandler(app, state, viewManager);
            return new LiveTestContext<RecipeCommandHandler>(state, viewManager, handler);
        });

        // Act
        await session.InvokeAsync((_, ctx) => ctx.Handler.LoadFromPathAsync(_recipeFile).AsTask());
        var (drillDown, actionStack) = await session.InvokeAsync((_, ctx) =>
            Task.FromResult((ctx.State.DrillDown, ctx.State.DrillDown?.ActionStack.ToArray())));

        // Assert — comparing the reference itself is safe (no dereference of its mutable fields);
        // ActionStack is read on the worker thread above instead of through this reference.
        drillDown.Should().BeSameAs(existingDrillDown);
        actionStack.Should().Equal(existingAction);
    }
}

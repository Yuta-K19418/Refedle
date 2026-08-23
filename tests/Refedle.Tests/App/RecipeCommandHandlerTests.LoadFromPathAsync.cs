using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Views;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Recipes;
using Refedle.Engine.Types;
using Terminal.Gui.Views;

namespace Refedle.Tests.App;

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
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = _jsonLinesFile };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var handler = new RecipeCommandHandler(app, state, viewManager);

        var action = new RenameColumnAction { OldName = "old", NewName = "new" };
        await SaveRecipeAsync(new Recipe { Name = "test", Actions = [action] }, _recipeFile);

        // Act
        app.Begin(window);
        await handler.LoadFromPathAsync(_recipeFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.ActionStack.Should().Equal(action);
    }

    [Fact]
    public async Task LoadFromPathAsync_JsonObjectRecipeWithMatchingEntry_RendersFocusedTableWithRecipeActionApplied()
    {
        // Arrange
        File.WriteAllText(_jsonObjectFile, """{"orders":[{"id":"A1"},{"id":"A2"}]}""");

        using var app = CreateTestApp();
        using var state = new AppState
        {
            CurrentFilePath = _jsonObjectFile,
            JsonObjectEntries = [new Refedle.Engine.IO.JsonObject.JsonObjectEntry(
                "orders", """[{"id":"A1"},{"id":"A2"}]"""u8.ToArray())],
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var handler = new RecipeCommandHandler(app, state, viewManager);

        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("orders", KeyPathSegmentKind.Key)];
        var action = new RenameColumnAction { OldName = "id", NewName = "orderId" };
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [action], DrillDownKeyPath = keyPath }, _recipeFile);

        // Act
        app.Begin(window);
        await handler.LoadFromPathAsync(_recipeFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        var drillDown = state.DrillDown.Should().BeOfType<DrillDownState>().Which;
        drillDown.KeyPath.Should().Equal(keyPath);
        drillDown.ActionStack.Should().Equal(action);

        var focusedView = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        var transformer = focusedView.Table.Should().BeOfType<FocusedTableTransformer>().Which;
        transformer.ColumnNames.Should().ContainSingle(name => name.StartsWith("orderId", StringComparison.Ordinal));
        transformer.ColumnNames.Should().NotContain(name => name.StartsWith("id ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadFromPathAsync_JsonLinesRecipe_RendersFocusedTableWithRecipeActionApplied()
    {
        // Arrange
        File.WriteAllText(_jsonLinesFile, "{\"user\":{\"name\":\"Alice\"}}\n{\"user\":{\"name\":\"Bob\"}}");

        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = _jsonLinesFile };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var handler = new RecipeCommandHandler(app, state, viewManager);

        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("user", KeyPathSegmentKind.Key)];
        var action = new RenameColumnAction { OldName = "name", NewName = "fullName" };
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [action], DrillDownKeyPath = keyPath }, _recipeFile);

        // Act
        app.Begin(window);
        await handler.LoadFromPathAsync(_recipeFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        var drillDown = state.DrillDown.Should().BeOfType<DrillDownState>().Which;
        drillDown.KeyPath.Should().Equal(keyPath);
        drillDown.ActionStack.Should().Equal(action);

        var focusedView = viewManager.GetCurrentView().Should().BeOfType<FocusedTableView>().Which;
        var transformer = focusedView.Table.Should().BeOfType<FocusedTableTransformer>().Which;
        transformer.ColumnNames.Should().ContainSingle(name => name.StartsWith("fullName", StringComparison.Ordinal));
        transformer.ColumnNames.Should().NotContain(name => name.StartsWith("name ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadFromPathAsync_JsonObjectRecipeWithMissingFirstSegment_ShowsErrorInsteadOfLoadingBaseTable()
    {
        // Arrange — the recorded first segment no longer matches this file's top-level entries
        // (e.g. the file changed since the recipe was saved).
        File.WriteAllText(_jsonObjectFile, """{"orders":[{"id":"A1"}]}""");

        using var app = CreateTestApp();
        using var state = new AppState
        {
            CurrentFilePath = _jsonObjectFile,
            JsonObjectEntries = [new Refedle.Engine.IO.JsonObject.JsonObjectEntry(
                "orders", """[{"id":"A1"}]"""u8.ToArray())],
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var handler = new RecipeCommandHandler(app, state, viewManager);

        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("missing", KeyPathSegmentKind.Key)];
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [], DrillDownKeyPath = keyPath }, _recipeFile);

        // Act
        app.Begin(window);
        await handler.LoadFromPathAsync(_recipeFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        viewManager.GetCurrentView().Should().BeOfType<PlaceholderView>()
            .Which.Text.Should().Contain("not found");
    }

    [Fact]
    public async Task LoadFromPathAsync_JsonObjectRecipeWithEmptyDrillDownKeyPath_ShowsDistinctError()
    {
        // Arrange — an empty DrillDownKeyPath against a JSON Object file: e.g. hand-edited YAML,
        // or a Full Aggregation DrillDown recipe mistakenly loaded against a JSON Object file.
        File.WriteAllText(_jsonObjectFile, """{"orders":[{"id":"A1"}]}""");

        using var app = CreateTestApp();
        using var state = new AppState
        {
            CurrentFilePath = _jsonObjectFile,
            JsonObjectEntries = [new Refedle.Engine.IO.JsonObject.JsonObjectEntry(
                "orders", """[{"id":"A1"}]"""u8.ToArray())],
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var handler = new RecipeCommandHandler(app, state, viewManager);

        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [], DrillDownKeyPath = [] }, _recipeFile);

        // Act
        app.Begin(window);
        await handler.LoadFromPathAsync(_recipeFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        viewManager.GetCurrentView().Should().BeOfType<PlaceholderView>()
            .Which.Text.Should().Contain("empty");
    }

    [Fact]
    public async Task LoadFromPathAsync_DrillDownRecipeAgainstCsvFile_ShowsErrorInsteadOfCrashing()
    {
        // Arrange — CSV is not a Full Aggregation DrillDown format (only JSON Lines/Array are),
        // so this must surface an explicit error rather than reach FullAggregationScanner.Scan,
        // which throws UnreachableException for any other format.
        File.WriteAllText(_csvFile, "id,name\n1,Alice\n");

        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = _csvFile };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var handler = new RecipeCommandHandler(app, state, viewManager);

        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("user", KeyPathSegmentKind.Key)];
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [], DrillDownKeyPath = keyPath }, _recipeFile);

        // Act
        Func<Task> act = async () =>
        {
            app.Begin(window);
            await handler.LoadFromPathAsync(_recipeFile);
            app.StopAfterFirstIteration = true;
            app.Run(window);
        };

        // Assert
        await act.Should().NotThrowAsync();
        viewManager.GetCurrentView().Should().BeOfType<PlaceholderView>()
            .Which.Text.Should().Contain("Csv");
    }

    [Fact]
    public async Task LoadFromPathAsync_JsonLinesRecipeWithUnmatchedKeyPath_PreservesExistingDrillDownActionStackOnFailure()
    {
        // Arrange — the scan finds no matching rows, so FullAggregationDrillDownAsync fails; the
        // pre-existing DrillDown session (and its ActionStack) must be left untouched rather than
        // overwritten before the transition is known to succeed.
        File.WriteAllText(_jsonLinesFile, "{\"user\":{\"name\":\"Alice\"}}\n{\"user\":{\"name\":\"Bob\"}}");

        using var app = CreateTestApp();
        var existingSchema = new TableSchema { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }] };
        var existingAction = new RenameColumnAction { OldName = "existing", NewName = "renamed_existing" };
        var existingDrillDown = new DrillDownState(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            existingSchema,
            ViewMode.JsonLinesTree,
            KeyPath: [new KeyPathSegment("previous", KeyPathSegmentKind.Key)],
            ActionStack: [existingAction]);
        using var state = new AppState
        {
            CurrentFilePath = _jsonLinesFile,
            CurrentMode = ViewMode.FocusedTable,
            DrillDown = existingDrillDown,
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var handler = new RecipeCommandHandler(app, state, viewManager);

        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("missing", KeyPathSegmentKind.Key)];
        var recipeAction = new RenameColumnAction { OldName = "name", NewName = "fullName" };
        await SaveRecipeAsync(
            new Recipe { Name = "test", Actions = [recipeAction], DrillDownKeyPath = keyPath }, _recipeFile);

        // Act
        app.Begin(window);
        await handler.LoadFromPathAsync(_recipeFile);
        app.StopAfterFirstIteration = true;
        app.Run(window);

        // Assert
        state.DrillDown.Should().BeSameAs(existingDrillDown);
        state.DrillDown.Should().BeOfType<DrillDownState>().Which.ActionStack.Should().Equal(existingAction);
    }
}

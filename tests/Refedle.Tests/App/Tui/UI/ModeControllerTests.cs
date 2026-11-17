using System.Text;
using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.Workers;
using Refedle.Engine;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;
using Refedle.Tests.App.Tui.Workers.Schema;

namespace Refedle.Tests.App.Tui.UI;

public sealed class ModeControllerTests : IDisposable
{
    private readonly string _jsonlFilePath;

    public ModeControllerTests()
    {
        _jsonlFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
    }

    public void Dispose()
    {
        if (File.Exists(_jsonlFilePath))
        {
            File.Delete(_jsonlFilePath);
        }
    }

    [Fact]
    public void Constructor_WithNullState_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup required)

        // Act
        Func<ModeController> act = () => new ModeController(null!, action => action(), TestSchemaScannerFactories.JsonLines);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUiThreadInvoke_ThrowsArgumentNullException()
    {
        // Arrange
        using AppState state = new();

        // Act
        Func<ModeController> act = () => new ModeController(state, null!, TestSchemaScannerFactories.JsonLines);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullScannerFactory_ThrowsArgumentNullException()
    {
        // Arrange
        using AppState state = new();

        // Act
        Func<ModeController> act = () => new ModeController(state, action => action(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_FromTreeMode_PublishesRefinedSchemaThroughInvoke()
    {
        // Arrange
        await WriteRefinementFixtureAsync(_jsonlFilePath);
        TaskCompletionSource<TableSchema> refined = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        RowIndexer indexer = new(_jsonlFilePath);
        indexer.BuildIndex();
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        state.BeginIndexedTreeLoad(indexer);
        state.EnterJsonLinesTree();
        state.SetSchemaRefinedCallback(schema => refined.TrySetResult(schema));
        int invokeCount = 0;
        ModeController controller = new(
            state,
            action =>
            {
                Interlocked.Increment(ref invokeCount);
                action();
            },
            TestSchemaScannerFactories.JsonLines);

        // Act
        Result result = await controller.ToggleJsonLinesModeAsync();
        TableSchema refinedSchema = await refined.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        result.IsSuccess.Should().BeTrue();
        refinedSchema.Columns.Select(c => c.Name).Should().Contain("age");
        state.Schema.Should().BeSameAs(refinedSchema);
        Volatile.Read(ref invokeCount).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WithIndexingInProgress_StartsBackgroundScanOnlyAfterIndexingCompletes()
    {
        // Arrange
        await WriteRefinementFixtureAsync(_jsonlFilePath);
        GatedSchemaScanner scanner = new(CreateIdSchema());
        scanner.Release();
        RowIndexer indexer = new(_jsonlFilePath);
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        state.BeginIndexedTreeLoad(indexer);
        state.EnterJsonLinesTree();
        ModeController controller = new(state, action => action(), _ => scanner);

        // Act
        Result result = await controller.ToggleJsonLinesModeAsync();
        bool startedBeforeIndexing = scanner.BackgroundScanStarted.IsCompleted;
        indexer.BuildIndex();
        await scanner.BackgroundScanStarted.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        result.IsSuccess.Should().BeTrue();
        startedBeforeIndexing.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenScanIsCancelledBeforeUiDispatch_DoesNotPublishStaleSchema()
    {
        // Arrange
        await WriteRefinementFixtureAsync(_jsonlFilePath);
        RowIndexer indexer = new(_jsonlFilePath);
        indexer.BuildIndex();
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        state.BeginIndexedTreeLoad(indexer);
        state.EnterJsonLinesTree();
        List<Action> queuedInvokes = [];
        TaskCompletionSource posted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ModeController controller = new(
            state,
            action =>
            {
                queuedInvokes.Add(action);
                posted.TrySetResult();
            },
            TestSchemaScannerFactories.JsonLines);

        // Act
        Result result = await controller.ToggleJsonLinesModeAsync();
        await posted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Replace the session before the queued UI action runs; the file path stays
        // the scanned one, as in a same-file reopen.
        state.RenewCtsWithCancel();
        TableSchema replacementSchema = new()
        {
            Columns = [new Refedle.Engine.Models.ColumnSchema { Name = "id", Type = Refedle.Engine.Types.ColumnType.WholeNumber }],
            SourceFormat = Refedle.Engine.Types.DataFormat.JsonLines
        };
        state.ApplyRefinedSchema(replacementSchema);
        Refedle.Engine.Models.TableSchema? callbackSchema = null;
        state.SetSchemaRefinedCallback(schema => callbackSchema = schema);

        queuedInvokes[0].Invoke();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.Schema.Should().BeSameAs(replacementSchema);
        callbackSchema.Should().BeNull();
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenSessionIsReplacedDuringInitialScan_KeepsReplacementFileState()
    {
        // Arrange
        TableSchema scannedSchema = CreateIdSchema();
        GatedSchemaScanner scanner = new(scannedSchema);
        RowIndexer indexer = new(_jsonlFilePath);
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        state.BeginIndexedTreeLoad(indexer);
        state.EnterJsonLinesTree();
        int invokeCount = 0;
        ModeController controller = new(state, _ => Interlocked.Increment(ref invokeCount), _ => scanner);

        // Act
        Task<Result> toggleTask = controller.ToggleJsonLinesModeAsync().AsTask();
        await scanner.Started.WaitAsync(TimeSpan.FromSeconds(10));

        // Replace the session while the gated initial scan is still pending.
        state.RenewCtsWithCancel();
        TableSchema replacementSchema = CreateIdSchema();
        state.ApplyRefinedSchema(replacementSchema);
        scanner.Release();

        Result result = await toggleTask;

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.Schema.Should().BeSameAs(replacementSchema);
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
        Volatile.Read(ref invokeCount).Should().Be(0);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenInitialScanFailsAfterSessionReplacement_SuppressesStaleError()
    {
        // Arrange
        GatedSchemaScanner scanner = new(CreateIdSchema(), new InvalidOperationException("scan failed"));
        RowIndexer indexer = new(_jsonlFilePath);
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        state.BeginIndexedTreeLoad(indexer);
        state.EnterJsonLinesTree();
        ModeController controller = new(state, _ => { }, _ => scanner);

        // Act
        Task<Result> toggleTask = controller.ToggleJsonLinesModeAsync().AsTask();
        await scanner.Started.WaitAsync(TimeSpan.FromSeconds(10));

        // Replace the session, then let the old scan fail.
        state.RenewCtsWithCancel();
        TableSchema replacementSchema = CreateIdSchema();
        state.ApplyRefinedSchema(replacementSchema);
        scanner.Release();

        Result result = await toggleTask;

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.Schema.Should().BeSameAs(replacementSchema);
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
    }

    private static Refedle.Engine.Models.TableSchema CreateIdSchema() => new()
    {
        Columns = [new Refedle.Engine.Models.ColumnSchema { Name = "id", Type = Refedle.Engine.Types.ColumnType.WholeNumber }],
        SourceFormat = Refedle.Engine.Types.DataFormat.JsonLines
    };

    private static Refedle.Engine.Models.TableSchema CreateCsvSchema() => new()
    {
        Columns = [new Refedle.Engine.Models.ColumnSchema { Name = "id", Type = Refedle.Engine.Types.ColumnType.WholeNumber }],
        SourceFormat = Refedle.Engine.Types.DataFormat.Csv
    };

    /// <summary>
    /// Writes a JSON Lines fixture whose first 200 lines fit the initial scan (single
    /// "name" column) and whose remaining lines introduce an "age" column, so the
    /// background refinement must produce a schema that differs from the initial one.
    /// </summary>
    private static async Task WriteRefinementFixtureAsync(string path)
    {
        IEnumerable<string> initialLines = Enumerable.Range(0, 200).Select(i => $"{{\"name\":\"user{i}\"}}");
        IEnumerable<string> refinedLines = Enumerable.Range(0, 5).Select(i => $"{{\"name\":\"user{i}\",\"age\":{i}}}");
        string content = string.Join("\n", initialLines.Concat(refinedLines)) + "\n";
        await File.WriteAllTextAsync(path, content);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WithRowIndexerNull_DoesNothing()
    {
        // Arrange
        using AppState state = new();
        state.EnterJsonLinesTree();
        ModeController controller = new(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        Result result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WithUnrelatedMode_DoesNothing()
    {
        // Arrange
        IRowIndexer indexer = RowIndexerFactory.Create(Refedle.Engine.Types.DataFormat.Csv, _jsonlFilePath);
        TableSchema schema = CreateCsvSchema();
        using AppState state = new();
        state.CompleteCsvLoad(indexer, schema);
        ModeController controller = new(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        Result result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.CsvTable);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WithCachedSchema_ReusesSchema()
    {
        // Arrange
        TableSchema cachedSchema = new()
        {
            Columns = [new Refedle.Engine.Models.ColumnSchema { Name = "id", Type = Refedle.Engine.Types.ColumnType.WholeNumber }],
            SourceFormat = Refedle.Engine.Types.DataFormat.JsonLines
        };

        RowIndexer indexer = new(_jsonlFilePath);
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        state.BeginIndexedTreeLoad(indexer);
        state.ApplyRefinedSchema(cachedSchema);
        state.EnterJsonLinesTree();
        ModeController controller = new(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        Result result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
        // Should have reused the cached schema
        state.Schema.Should().BeSameAs(cachedSchema);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_FromTreeMode_ScansSchemaAndSwitchesToTable()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"name\":\"Alice\"}\n{\"name\":\"Bob\"}");
        RowIndexer indexer = new(_jsonlFilePath);
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        state.BeginIndexedTreeLoad(indexer);
        state.EnterJsonLinesTree();
        ModeController controller = new(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        Result result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
        state.Schema.Should().NotBeNull();
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_FromTableMode_RestoresTreeMode()
    {
        // Arrange
        TableSchema schema = CreateIdSchema();
        using AppState state = new();
        state.CompleteJsonLinesSchemaScan(schema);
        ModeController controller = new(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        Result result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(1)]
    public void DrillDown_JsonObjectWithChildren_PopulatesAllRowsAndSwitchesToFocusedTable(int childCount)
    {
        // Arrange
        string children = string.Join(",", Enumerable.Range(0, childCount).Select(i => $$"""{"id":{{i}}}"""));
        JsonRawBytes nodeBytes = Encoding.UTF8.GetBytes($"[{children}]");
        SingleDrillDownRequest request = new(
            Format: Refedle.Engine.Types.DataFormat.JsonObject,
            NodeBytes: nodeBytes,
            KeyPath: [],
            InitialActionStack: []);
        using AppState state = new();
        ModeController controller = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        IEnumerable<string> expectedHashValues = Enumerable.Range(0, childCount).Select(i => $"[{i}]");

        // Act
        Result result = controller.DrillDown(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which.Rows.Select(r => r.HashValue).Should().Equal(expectedHashValues);
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_ValidJsonLinesFile_ReturnsSuccessWithoutMutatingState()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _jsonlFilePath, "{\"user\":{\"name\":\"Alice\"}}\n{\"user\":{\"name\":\"Bob\"}}");
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        ModeController controller = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        FullAggregationDrillDownRequest request = new(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: [new KeyPathSegment("user", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        Result<DrillDownState> result = await controller.FullAggregationDrillDownAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().HaveCount(2);
        result.Value.Schema.Columns.Select(c => c.Name).Should().Equal("name");
        state.GetDrillDownOrNull().Should().BeNull();
        state.CurrentMode.Should().Be(ViewMode.FileSelection);
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_ScanFails_ReturnsFailureWithoutMutatingState()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"user\":{\"name\":\"Alice\"}}");
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        ModeController controller = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        FullAggregationDrillDownRequest request = new(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: [new KeyPathSegment("missing", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        Result<DrillDownState> result = await controller.FullAggregationDrillDownAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No matching records found.");
        state.GetDrillDownOrNull().Should().BeNull();
        state.CurrentMode.Should().Be(ViewMode.FileSelection);
    }

    [Fact]
    public void DrillDown_WhenCalledFromJsonObjectTree_CapturesPreviousModeAsJsonObjectTree()
    {
        // Arrange
        JsonRawBytes nodeBytes = Encoding.UTF8.GetBytes("""[{"id":1}]""");
        SingleDrillDownRequest request = new(
            Format: Refedle.Engine.Types.DataFormat.JsonObject,
            NodeBytes: nodeBytes,
            KeyPath: [],
            InitialActionStack: []);
        using AppState state = new();
        state.EnterJsonObjectTree([]);
        ModeController controller = new(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        Result result = controller.DrillDown(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>()
            .Which.PreviousMode.Should().Be(ViewMode.JsonObjectTree);
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WhenCalledFromJsonLinesTree_CapturesPreviousModeAsJsonLinesTree()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _jsonlFilePath, "{\"user\":{\"name\":\"Alice\"}}");
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        state.EnterJsonLinesTree();
        ModeController controller = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        FullAggregationDrillDownRequest request = new(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: [new KeyPathSegment("user", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        Result<DrillDownState> result = await controller.FullAggregationDrillDownAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PreviousMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WhenStartedOnWorkerThread_UsesStateAsOfCallbackExecution()
    {
        // Arrange
        string initialPath = _jsonlFilePath;
        string laterPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jsonl");
        try
        {
            await File.WriteAllTextAsync(initialPath, "{\"user\":{\"name\":\"Alice\"}}");
            await File.WriteAllTextAsync(laterPath, "{\"user\":{\"name\":\"Bob\"}}\n{\"user\":{\"name\":\"Carol\"}}");
            using AppState state = new();
            state.StartNewFile(initialPath);
            state.EnterPlaceholderMode();
            TaskCompletionSource<Action> callbackHandoff = new(TaskCreationOptions.RunContinuationsAsynchronously);
            ModeController controller = new(
                state, action => callbackHandoff.TrySetResult(action), TestSchemaScannerFactories.JsonLines);
            FullAggregationDrillDownRequest request = new(
                Format: Refedle.Engine.Types.DataFormat.JsonLines,
                KeyPath: [new KeyPathSegment("user", KeyPathSegmentKind.Key)],
                InitialActionStack: []);

            // Act
            Task<Result<DrillDownState>> pending = Task.Run(() => controller.FullAggregationDrillDownAsync(request).AsTask());
            Action callback = await callbackHandoff.Task.WaitAsync(TimeSpan.FromSeconds(15));
            state.StartNewFile(laterPath);
            state.EnterJsonLinesTree();
            callback();
            Result<DrillDownState> result = await pending.WaitAsync(TimeSpan.FromSeconds(15));

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Rows.Should().HaveCount(2);
            result.Value.PreviousMode.Should().Be(ViewMode.JsonLinesTree);
        }
        finally
        {
            File.Delete(laterPath);
        }
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WhenSessionRenewedBeforeCallbackExecution_SucceedsWithReplacementSession()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"user\":{\"name\":\"Alice\"}}");
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        TaskCompletionSource<Action> callbackHandoff = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ModeController controller = new(
            state, action => callbackHandoff.TrySetResult(action), TestSchemaScannerFactories.JsonLines);
        FullAggregationDrillDownRequest request = new(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: [new KeyPathSegment("user", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        Task<Result<DrillDownState>> pending = Task.Run(() => controller.FullAggregationDrillDownAsync(request).AsTask());
        Action callback = await callbackHandoff.Task.WaitAsync(TimeSpan.FromSeconds(15));
        state.RenewCtsWithCancel();
        callback();
        Result<DrillDownState> result = await pending.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().HaveCount(1);
    }

    [Fact]
    public void DrillDown_WithKeyPathOnRequest_PopulatesDrillDownStateKeyPath()
    {
        // Arrange
        JsonRawBytes nodeBytes = Encoding.UTF8.GetBytes("""[{"id":1}]""");
        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("orders", KeyPathSegmentKind.Key)];
        SingleDrillDownRequest request = new(
            Format: Refedle.Engine.Types.DataFormat.JsonObject,
            NodeBytes: nodeBytes,
            KeyPath: keyPath,
            InitialActionStack: []);
        using AppState state = new();
        ModeController controller = new(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        Result result = controller.DrillDown(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.GetDrillDownOrNull().Should().BeOfType<DrillDownState>().Which.KeyPath.Should().Equal(keyPath);
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WithKeyPathOnRequest_PopulatesDrillDownStateKeyPath()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _jsonlFilePath, "{\"user\":{\"name\":\"Alice\"}}");
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        ModeController controller = new(state, action => action(), TestSchemaScannerFactories.JsonLines);
        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("user", KeyPathSegmentKind.Key)];
        FullAggregationDrillDownRequest request = new(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: keyPath,
            InitialActionStack: []);

        // Act
        Result<DrillDownState> result = await controller.FullAggregationDrillDownAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.KeyPath.Should().Equal(keyPath);
    }
}

using System.Text;
using AwesomeAssertions;
using Refedle.App;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonLines;
using Refedle.Tests.App.Schema;

namespace Refedle.Tests.App;

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
        var act = () => new ModeController(null!, action => action(), TestSchemaScannerFactories.JsonLines);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUiThreadInvoke_ThrowsArgumentNullException()
    {
        // Arrange
        using var state = new AppState();

        // Act
        var act = () => new ModeController(state, null!, TestSchemaScannerFactories.JsonLines);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullScannerFactory_ThrowsArgumentNullException()
    {
        // Arrange
        using var state = new AppState();

        // Act
        var act = () => new ModeController(state, action => action(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_FromTreeMode_PublishesRefinedSchemaThroughInvoke()
    {
        // Arrange
        await WriteRefinementFixtureAsync(_jsonlFilePath);
        var refined = new TaskCompletionSource<Refedle.Engine.Models.TableSchema>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var indexer = new RowIndexer(_jsonlFilePath);
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        state.BeginIndexedTreeLoad(indexer);
        state.EnterJsonLinesTree();
        state.SetSchemaRefinedCallback(schema => refined.TrySetResult(schema));
        var invokeCount = 0;
        var controller = new ModeController(
            state,
            action =>
            {
                Interlocked.Increment(ref invokeCount);
                action();
            },
            TestSchemaScannerFactories.JsonLines);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();
        var refinedSchema = await refined.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        result.IsSuccess.Should().BeTrue();
        refinedSchema.Columns.Select(c => c.Name).Should().Contain("age");
        state.Schema.Should().BeSameAs(refinedSchema);
        Volatile.Read(ref invokeCount).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_BackgroundSchemaCompletes_RaisesSchemaRefinedOnUiThread()
    {
        // Arrange
        await WriteRefinementFixtureAsync(_jsonlFilePath);
        var refined = new TaskCompletionSource<Refedle.Engine.Models.TableSchema>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackThreadId = 0;
        var uiThreadId = 0;
        await using var session = await LivePumpTestSession.StartAsync((app, _) =>
        {
            var indexer = new RowIndexer(_jsonlFilePath);
            var state = new AppState();
            state.StartNewFile(_jsonlFilePath);
            state.BeginIndexedTreeLoad(indexer);
            state.EnterJsonLinesTree();
            state.SetSchemaRefinedCallback(schema =>
            {
                callbackThreadId = Environment.CurrentManagedThreadId;
                refined.TrySetResult(schema);
            });
            return new ModeController(state, app.Invoke, TestSchemaScannerFactories.JsonLines);
        });

        // Act
        await session.InvokeAsync((_, controller) =>
        {
            uiThreadId = Environment.CurrentManagedThreadId;
            return controller.ToggleJsonLinesModeAsync().AsTask();
        });
        var refinedSchema = await refined.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        callbackThreadId.Should().Be(uiThreadId);
        refinedSchema.Columns.Select(c => c.Name).Should().Contain("age");
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenScanIsCancelledBeforeUiDispatch_DoesNotPublishStaleSchema()
    {
        // Arrange
        await WriteRefinementFixtureAsync(_jsonlFilePath);
        var indexer = new RowIndexer(_jsonlFilePath);
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        state.BeginIndexedTreeLoad(indexer);
        state.EnterJsonLinesTree();
        var queuedInvokes = new List<Action>();
        var posted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new ModeController(
            state,
            action =>
            {
                queuedInvokes.Add(action);
                posted.TrySetResult();
            },
            TestSchemaScannerFactories.JsonLines);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();
        await posted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Replace the session before the queued UI action runs; the file path stays
        // the scanned one, as in a same-file reopen.
        state.RenewCtsWithCancel();
        var replacementSchema = new Refedle.Engine.Models.TableSchema
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
        var scannedSchema = CreateIdSchema();
        var scanner = new GatedSchemaScanner(scannedSchema);
        var indexer = new RowIndexer(_jsonlFilePath);
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        state.BeginIndexedTreeLoad(indexer);
        state.EnterJsonLinesTree();
        var invokeCount = 0;
        var controller = new ModeController(state, _ => Interlocked.Increment(ref invokeCount), _ => scanner);

        // Act
        var toggleTask = controller.ToggleJsonLinesModeAsync().AsTask();
        await scanner.Started.WaitAsync(TimeSpan.FromSeconds(10));

        // Replace the session while the gated initial scan is still pending.
        state.RenewCtsWithCancel();
        var replacementSchema = CreateIdSchema();
        state.ApplyRefinedSchema(replacementSchema);
        scanner.Release();

        var result = await toggleTask;

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
        var scanner = new GatedSchemaScanner(CreateIdSchema(), new InvalidOperationException("scan failed"));
        var indexer = new RowIndexer(_jsonlFilePath);
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        state.BeginIndexedTreeLoad(indexer);
        state.EnterJsonLinesTree();
        var controller = new ModeController(state, _ => { }, _ => scanner);

        // Act
        var toggleTask = controller.ToggleJsonLinesModeAsync().AsTask();
        await scanner.Started.WaitAsync(TimeSpan.FromSeconds(10));

        // Replace the session, then let the old scan fail.
        state.RenewCtsWithCancel();
        var replacementSchema = CreateIdSchema();
        state.ApplyRefinedSchema(replacementSchema);
        scanner.Release();

        var result = await toggleTask;

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
        var initialLines = Enumerable.Range(0, 200).Select(i => $"{{\"name\":\"user{i}\"}}");
        var refinedLines = Enumerable.Range(0, 5).Select(i => $"{{\"name\":\"user{i}\",\"age\":{i}}}");
        var content = string.Join("\n", initialLines.Concat(refinedLines)) + "\n";
        await File.WriteAllTextAsync(path, content);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WithRowIndexerNull_DoesNothing()
    {
        // Arrange
        using var state = new AppState();
        state.EnterJsonLinesTree();
        var controller = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WithUnrelatedMode_DoesNothing()
    {
        // Arrange
        var indexer = RowIndexerFactory.Create(Refedle.Engine.Types.DataFormat.Csv, _jsonlFilePath);
        var schema = CreateCsvSchema();
        using var state = new AppState();
        state.CompleteCsvLoad(indexer, schema);
        var controller = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.CsvTable);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WithCachedSchema_ReusesSchema()
    {
        // Arrange
        var cachedSchema = new Refedle.Engine.Models.TableSchema
        {
            Columns = [new Refedle.Engine.Models.ColumnSchema { Name = "id", Type = Refedle.Engine.Types.ColumnType.WholeNumber }],
            SourceFormat = Refedle.Engine.Types.DataFormat.JsonLines
        };

        var indexer = new RowIndexer(_jsonlFilePath);
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        state.BeginIndexedTreeLoad(indexer);
        state.ApplyRefinedSchema(cachedSchema);
        state.EnterJsonLinesTree();
        var controller = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

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
        var indexer = new RowIndexer(_jsonlFilePath);
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        state.BeginIndexedTreeLoad(indexer);
        state.EnterJsonLinesTree();
        var controller = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
        state.Schema.Should().NotBeNull();
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_FromTableMode_RestoresTreeMode()
    {
        // Arrange
        var schema = CreateIdSchema();
        using var state = new AppState();
        state.CompleteJsonLinesSchemaScan(schema);
        var controller = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

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
        var children = string.Join(",", Enumerable.Range(0, childCount).Select(i => $$"""{"id":{{i}}}"""));
        JsonRawBytes nodeBytes = Encoding.UTF8.GetBytes($"[{children}]");
        var request = new SingleDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonObject,
            NodeBytes: nodeBytes,
            KeyPath: [],
            InitialActionStack: []);
        using var state = new AppState();
        var controller = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        var expectedHashValues = Enumerable.Range(0, childCount).Select(i => $"[{i}]");

        // Act
        var result = controller.DrillDown(request);

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
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        var controller = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        var request = new FullAggregationDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: [new KeyPathSegment("user", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        var result = await controller.FullAggregationDrillDownAsync(request);

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
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        var controller = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        var request = new FullAggregationDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: [new KeyPathSegment("missing", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        var result = await controller.FullAggregationDrillDownAsync(request);

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
        var request = new SingleDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonObject,
            NodeBytes: nodeBytes,
            KeyPath: [],
            InitialActionStack: []);
        using var state = new AppState();
        state.EnterJsonObjectTree([]);
        var controller = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        var result = controller.DrillDown(request);

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
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        state.EnterJsonLinesTree();
        var controller = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        var request = new FullAggregationDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: [new KeyPathSegment("user", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        var result = await controller.FullAggregationDrillDownAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PreviousMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Fact]
    public void DrillDown_WithKeyPathOnRequest_PopulatesDrillDownStateKeyPath()
    {
        // Arrange
        JsonRawBytes nodeBytes = Encoding.UTF8.GetBytes("""[{"id":1}]""");
        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("orders", KeyPathSegmentKind.Key)];
        var request = new SingleDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonObject,
            NodeBytes: nodeBytes,
            KeyPath: keyPath,
            InitialActionStack: []);
        using var state = new AppState();
        var controller = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);

        // Act
        var result = controller.DrillDown(request);

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
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        var controller = new ModeController(state, action => action(), TestSchemaScannerFactories.JsonLines);
        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("user", KeyPathSegmentKind.Key)];
        var request = new FullAggregationDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: keyPath,
            InitialActionStack: []);

        // Act
        var result = await controller.FullAggregationDrillDownAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.KeyPath.Should().Equal(keyPath);
    }
}

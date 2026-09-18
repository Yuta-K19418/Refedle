using System.Text;
using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Schema;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.App;

[Collection("IncrementalSchemaScannerHook")]
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
        var act = () => new ModeController(null!, action => action());

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUiThreadInvoke_ThrowsArgumentNullException()
    {
        // Arrange
        using var state = new AppState();

        // Act
        var act = () => new ModeController(state, null!);

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
        using var state = new AppState
        {
            CurrentFilePath = _jsonlFilePath,
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = new RowIndexer(_jsonlFilePath),
            OnSchemaRefined = schema => refined.TrySetResult(schema)
        };
        var invokeCount = 0;
        var controller = new ModeController(
            state,
            action =>
            {
                Interlocked.Increment(ref invokeCount);
                action();
            });

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
            var state = new AppState
            {
                CurrentFilePath = _jsonlFilePath,
                CurrentMode = ViewMode.JsonLinesTree,
                RowIndexer = new RowIndexer(_jsonlFilePath),
                OnSchemaRefined = schema =>
                {
                    callbackThreadId = Environment.CurrentManagedThreadId;
                    refined.TrySetResult(schema);
                }
            };
            return new ModeController(state, app.Invoke);
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
        using var state = new AppState
        {
            CurrentFilePath = _jsonlFilePath,
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = new RowIndexer(_jsonlFilePath)
        };
        var queuedInvokes = new List<Action>();
        var posted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new ModeController(
            state,
            action =>
            {
                queuedInvokes.Add(action);
                posted.TrySetResult();
            });

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();
        await posted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Simulate loading a different file: the CTS is renewed with cancel and the
        // state moves to another file before the queued UI action runs.
        state.RenewCtsWithCancel();
        var replacementSchema = new Refedle.Engine.Models.TableSchema
        {
            Columns = [new Refedle.Engine.Models.ColumnSchema { Name = "id", Type = Refedle.Engine.Types.ColumnType.WholeNumber }],
            SourceFormat = Refedle.Engine.Types.DataFormat.JsonLines
        };
        state.CurrentFilePath = "replacement.jsonl";
        state.Schema = replacementSchema;
        Refedle.Engine.Models.TableSchema? callbackSchema = null;
        state.OnSchemaRefined = schema => callbackSchema = schema;

        queuedInvokes[0].Invoke();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.Schema.Should().BeSameAs(replacementSchema);
        callbackSchema.Should().BeNull();
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenFileIsSwitchedDuringInitialScan_KeepsReplacementFileState()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"name\":\"Alice\"}");
        var scanStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var scanGate = new ManualResetEventSlim(false);
        IncrementalSchemaScannerBase.ScanStartedHook = path =>
        {
            if (path == _jsonlFilePath)
            {
                scanStarted.TrySetResult();
                scanGate.Wait();
            }
        };
        using var state = new AppState
        {
            CurrentFilePath = _jsonlFilePath,
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = new RowIndexer(_jsonlFilePath)
        };
        var invokeCount = 0;
        var controller = new ModeController(state, _ => Interlocked.Increment(ref invokeCount));

        try
        {
            // Act
            var toggleTask = controller.ToggleJsonLinesModeAsync().AsTask();
            await scanStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Switch to another file while the gated initial scan is still pending.
            state.RenewCtsWithCancel();
            var replacementSchema = new Refedle.Engine.Models.TableSchema
            {
                Columns = [new Refedle.Engine.Models.ColumnSchema { Name = "id", Type = Refedle.Engine.Types.ColumnType.WholeNumber }],
                SourceFormat = Refedle.Engine.Types.DataFormat.JsonLines
            };
            state.CurrentFilePath = "replacement.jsonl";
            state.Schema = replacementSchema;
            scanGate.Set();

            var result = await toggleTask;

            // Assert
            result.IsSuccess.Should().BeTrue();
            state.Schema.Should().BeSameAs(replacementSchema);
            state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
            Volatile.Read(ref invokeCount).Should().Be(0);
        }
        finally
        {
            IncrementalSchemaScannerBase.ScanStartedHook = null;
        }
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenInitialScanFailsAfterFileSwitch_SuppressesStaleError()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"name\":\"Alice\"}");
        var scanStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var scanGate = new ManualResetEventSlim(false);
        IncrementalSchemaScannerBase.ScanStartedHook = path =>
        {
            if (path == _jsonlFilePath)
            {
                scanStarted.TrySetResult();
                scanGate.Wait();
            }
        };
        using var state = new AppState
        {
            CurrentFilePath = _jsonlFilePath,
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = new RowIndexer(_jsonlFilePath)
        };
        var controller = new ModeController(state, _ => { });

        try
        {
            // Act
            var toggleTask = controller.ToggleJsonLinesModeAsync().AsTask();
            await scanStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Make the old scan fail by deleting its file, then switch to another session.
            File.Delete(_jsonlFilePath);
            state.RenewCtsWithCancel();
            var replacementSchema = new Refedle.Engine.Models.TableSchema
            {
                Columns = [new Refedle.Engine.Models.ColumnSchema { Name = "id", Type = Refedle.Engine.Types.ColumnType.WholeNumber }],
                SourceFormat = Refedle.Engine.Types.DataFormat.JsonLines
            };
            state.CurrentFilePath = "replacement.jsonl";
            state.Schema = replacementSchema;
            scanGate.Set();

            var result = await toggleTask;

            // Assert
            result.IsSuccess.Should().BeTrue();
            state.Schema.Should().BeSameAs(replacementSchema);
            state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
        }
        finally
        {
            IncrementalSchemaScannerBase.ScanStartedHook = null;
        }
    }

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
        using var state = new AppState
        {
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = null
        };
        var controller = new ModeController(state, action => action());

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
        using var state = new AppState
        {
            CurrentMode = ViewMode.CsvTable
        };
        var controller = new ModeController(state, action => action());

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

        using var state = new AppState
        {
            CurrentFilePath = _jsonlFilePath,
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = new RowIndexer(_jsonlFilePath),
            Schema = cachedSchema
        };
        var controller = new ModeController(state, action => action());

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
        using var state = new AppState
        {
            CurrentFilePath = _jsonlFilePath,
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = new RowIndexer(_jsonlFilePath)
        };
        var controller = new ModeController(state, action => action());

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
        using var state = new AppState
        {
            CurrentMode = ViewMode.JsonLinesTable
        };
        var controller = new ModeController(state, action => action());

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
        var controller = new ModeController(state, action => action());
        var expectedHashValues = Enumerable.Range(0, childCount).Select(i => $"[{i}]");

        // Act
        var result = controller.DrillDown(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        state.DrillDown.Should().BeOfType<DrillDownState>().Which.Rows.Select(r => r.HashValue).Should().Equal(expectedHashValues);
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_ValidJsonLinesFile_ReturnsSuccessWithoutMutatingState()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _jsonlFilePath, "{\"user\":{\"name\":\"Alice\"}}\n{\"user\":{\"name\":\"Bob\"}}");
        using var state = new AppState { CurrentFilePath = _jsonlFilePath };
        var controller = new ModeController(state, action => action());
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
        state.DrillDown.Should().BeNull();
        state.CurrentMode.Should().Be(ViewMode.FileSelection);
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_ScanFails_ReturnsFailureWithoutMutatingState()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"user\":{\"name\":\"Alice\"}}");
        using var state = new AppState { CurrentFilePath = _jsonlFilePath };
        var controller = new ModeController(state, action => action());
        var request = new FullAggregationDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: [new KeyPathSegment("missing", KeyPathSegmentKind.Key)],
            InitialActionStack: []);

        // Act
        var result = await controller.FullAggregationDrillDownAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No matching records found.");
        state.DrillDown.Should().BeNull();
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
        using var state = new AppState { CurrentMode = ViewMode.JsonObjectTree };
        var controller = new ModeController(state, action => action());

        // Act
        var result = controller.DrillDown(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.DrillDown.Should().BeOfType<DrillDownState>()
            .Which.PreviousMode.Should().Be(ViewMode.JsonObjectTree);
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WhenCalledFromJsonLinesTree_CapturesPreviousModeAsJsonLinesTree()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _jsonlFilePath, "{\"user\":{\"name\":\"Alice\"}}");
        using var state = new AppState
        {
            CurrentFilePath = _jsonlFilePath,
            CurrentMode = ViewMode.JsonLinesTree,
        };
        var controller = new ModeController(state, action => action());
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
        var controller = new ModeController(state, action => action());

        // Act
        var result = controller.DrillDown(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.DrillDown.Should().BeOfType<DrillDownState>().Which.KeyPath.Should().Equal(keyPath);
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WithKeyPathOnRequest_PopulatesDrillDownStateKeyPath()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _jsonlFilePath, "{\"user\":{\"name\":\"Alice\"}}");
        using var state = new AppState { CurrentFilePath = _jsonlFilePath };
        var controller = new ModeController(state, action => action());
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

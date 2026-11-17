using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.Workers.Schema;
using Refedle.App.Tui.Workers.Schema.JsonLines;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Tui.Workers.Schema;

public sealed class BackgroundSchemaRefinerTests : IDisposable
{
    private readonly string _jsonlFilePath;

    public BackgroundSchemaRefinerTests()
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
    public async Task Start_WithFreshSession_PublishesRefinedSchemaThroughUiThreadInvoke()
    {
        // Arrange
        await WriteRefinementFixtureAsync();
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        IncrementalSchemaScanner scanner = new(_jsonlFilePath);
        TableSchema initialSchema = await scanner.InitialScanAsync();
        TaskCompletionSource<TableSchema> refined = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        state.SetSchemaRefinedCallback(schema => refined.TrySetResult(schema));
        int invokeCount = 0;

        // Act
        Task continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            action =>
            {
                Interlocked.Increment(ref invokeCount);
                action();
            },
            Task.CompletedTask,
            state.Cts.Token);
        TableSchema refinedSchema = await refined.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await continuation;

        // Assert
        refinedSchema.Columns.Select(c => c.Name).Should().Contain("age");
        state.Schema.Should().BeSameAs(refinedSchema);
        Volatile.Read(ref invokeCount).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Start_WhenSessionIsReplacedAfterUiPost_DoesNotPublishStaleSchema()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"name\":\"Alice\"}");
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        IncrementalSchemaScanner scanner = new(_jsonlFilePath);
        TableSchema initialSchema = await scanner.InitialScanAsync();
        List<Action> queuedInvokes = [];
        TaskCompletionSource posted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        Task continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            action =>
            {
                queuedInvokes.Add(action);
                posted.TrySetResult();
            },
            Task.CompletedTask,
            state.Cts.Token);
        await posted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Replace the session after the UI post was queued but before it ran; the file
        // path stays the scanned one, as in a same-file reopen.
        state.RenewCtsWithCancel();
        TableSchema replacementSchema = new()
        {
            Columns = [new ColumnSchema { Name = "id", Type = ColumnType.WholeNumber }],
            SourceFormat = DataFormat.JsonLines
        };
        state.ApplyRefinedSchema(replacementSchema);
        TableSchema? callbackSchema = null;
        state.SetSchemaRefinedCallback(schema => callbackSchema = schema);

        queuedInvokes[0].Invoke();
        await continuation;

        // Assert
        state.Schema.Should().BeSameAs(replacementSchema);
        callbackSchema.Should().BeNull();
    }

    [Fact]
    public async Task Start_WithAlreadyCancelledToken_NeverInvokesUiThreadInvoke()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"name\":\"Alice\"}");
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        IncrementalSchemaScanner scanner = new(_jsonlFilePath);
        TableSchema initialSchema = await scanner.InitialScanAsync();
        CancellationToken cancelledToken = state.Cts.Token;
        state.RenewCtsWithCancel();
        int invokeCount = 0;

        // Act
        Task continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            _ => Interlocked.Increment(ref invokeCount),
            Task.CompletedTask,
            cancelledToken);
        await continuation;

        // Assert
        Volatile.Read(ref invokeCount).Should().Be(0);
    }

    [Fact]
    public async Task Start_WithFaultedBackgroundScan_CompletesWithoutInvokingUiThreadInvoke()
    {
        // Arrange
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        TableSchema initialSchema = CreateIdSchema();
        GatedSchemaScanner scanner = new(
            initialSchema,
            backgroundFailure: new InvalidOperationException("background scan failed"));
        int invokeCount = 0;

        // Act
        Task continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            _ => Interlocked.Increment(ref invokeCount),
            Task.CompletedTask,
            state.Cts.Token);
        // Awaiting verifies the returned task completes without propagating the scanner fault.
        await continuation;

        // Assert
        Volatile.Read(ref invokeCount).Should().Be(0);
    }

    [Fact]
    public async Task Start_WithCanceledBackgroundScan_CompletesWithoutInvokingUiThreadInvoke()
    {
        // Arrange
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        TableSchema initialSchema = CreateIdSchema();
        GatedSchemaScanner scanner = new(initialSchema, backgroundCanceled: true);
        int invokeCount = 0;

        // Act
        Task continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            _ => Interlocked.Increment(ref invokeCount),
            Task.CompletedTask,
            state.Cts.Token);
        // Awaiting verifies the returned task completes without propagating the scan cancellation.
        await continuation;

        // Assert
        Volatile.Read(ref invokeCount).Should().Be(0);
    }

    [Fact]
    public void Start_WithNullState_ThrowsArgumentNullException()
    {
        // Arrange
        TableSchema initialSchema = CreateIdSchema();
        GatedSchemaScanner scanner = new(initialSchema);

        // Act
        // A statement body keeps the lambda Action-returning, so Throw asserts a synchronous throw.
        Action act = () =>
        {
            BackgroundSchemaRefiner.StartAsync(
                null!,
                scanner,
                initialSchema,
                _ => { },
                Task.CompletedTask,
                CancellationToken.None);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("state");
    }

    [Fact]
    public void Start_WithNullScanner_ThrowsArgumentNullException()
    {
        // Arrange
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        TableSchema initialSchema = CreateIdSchema();

        // Act
        // A statement body keeps the lambda Action-returning, so Throw asserts a synchronous throw.
        Action act = () =>
        {
            BackgroundSchemaRefiner.StartAsync(
                state,
                null!,
                initialSchema,
                _ => { },
                Task.CompletedTask,
            state.Cts.Token);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("scanner");
    }

    [Fact]
    public void Start_WithNullInitialSchema_ThrowsArgumentNullException()
    {
        // Arrange
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        GatedSchemaScanner scanner = new(CreateIdSchema());

        // Act
        // A statement body keeps the lambda Action-returning, so Throw asserts a synchronous throw.
        Action act = () =>
        {
            BackgroundSchemaRefiner.StartAsync(
                state,
                scanner,
                null!,
                _ => { },
                Task.CompletedTask,
            state.Cts.Token);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("initialSchema");
    }

    [Fact]
    public void Start_WithNullUiThreadInvoke_ThrowsArgumentNullException()
    {
        // Arrange
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        TableSchema initialSchema = CreateIdSchema();
        GatedSchemaScanner scanner = new(initialSchema);

        // Act
        // A statement body keeps the lambda Action-returning, so Throw asserts a synchronous throw.
        Action act = () =>
        {
            BackgroundSchemaRefiner.StartAsync(
                state,
                scanner,
                initialSchema,
                null!,
                Task.CompletedTask,
            state.Cts.Token);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("uiThreadInvoke");
    }

    [Fact]
    public async Task Start_WithPendingIndexingCompleted_DoesNotStartBackgroundScanUntilIndexingCompletes()
    {
        // Arrange
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        TableSchema initialSchema = CreateIdSchema();
        GatedSchemaScanner scanner = new(initialSchema);
        TaskCompletionSource indexingCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        Task continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            action => action(),
            indexingCompleted.Task,
            state.Cts.Token);
        bool startedBeforeCompletion = scanner.BackgroundScanStarted.IsCompleted;
        indexingCompleted.SetResult();
        await scanner.BackgroundScanStarted.WaitAsync(TimeSpan.FromSeconds(10));
        await continuation.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        startedBeforeCompletion.Should().BeFalse();
    }

    [Fact]
    public async Task Start_WhenCancelledWhileWaitingForIndexing_CompletesWithoutInvokingUiThreadInvoke()
    {
        // Arrange
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        TableSchema initialSchema = CreateIdSchema();
        GatedSchemaScanner scanner = new(initialSchema);
        TaskCompletionSource indexingCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int invokeCount = 0;
        Task continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            _ => Interlocked.Increment(ref invokeCount),
            indexingCompleted.Task,
            state.Cts.Token);

        // Act
        state.RenewCtsWithCancel();
        // Awaiting verifies the returned task completes without propagating the cancellation.
        await continuation.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Volatile.Read(ref invokeCount).Should().Be(0);
        scanner.BackgroundScanStarted.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void Start_WithNullIndexingCompleted_ThrowsArgumentNullException()
    {
        // Arrange
        using AppState state = new();
        state.StartNewFile(_jsonlFilePath);
        TableSchema initialSchema = CreateIdSchema();
        GatedSchemaScanner scanner = new(initialSchema);

        // Act
        // A statement body keeps the lambda Action-returning, so Throw asserts a synchronous throw.
        Action act = () =>
        {
            BackgroundSchemaRefiner.StartAsync(
                state,
                scanner,
                initialSchema,
                _ => { },
                null!,
                state.Cts.Token);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("indexingCompleted");
    }

    /// <summary>
    /// Writes a JSON Lines fixture whose first 200 lines fit the initial scan (single
    /// "name" column) and whose remaining lines introduce an "age" column, so the
    /// background refinement must produce a schema that differs from the initial one.
    /// </summary>
    private async Task WriteRefinementFixtureAsync()
    {
        IEnumerable<string> initialLines = Enumerable.Range(0, 200).Select(i => $"{{\"name\":\"user{i}\"}}");
        IEnumerable<string> refinedLines = Enumerable.Range(0, 5).Select(i => $"{{\"name\":\"user{i}\",\"age\":{i}}}");
        string content = string.Join("\n", initialLines.Concat(refinedLines)) + "\n";
        await File.WriteAllTextAsync(_jsonlFilePath, content);
    }

    private static TableSchema CreateIdSchema() => new()
    {
        Columns = [new ColumnSchema { Name = "id", Type = ColumnType.WholeNumber }],
        SourceFormat = DataFormat.JsonLines
    };
}

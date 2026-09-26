using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Schema;
using Refedle.App.Schema.JsonLines;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Schema;

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
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        var scanner = new IncrementalSchemaScanner(_jsonlFilePath);
        var initialSchema = await scanner.InitialScanAsync();
        var refined = new TaskCompletionSource<TableSchema>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        state.SetSchemaRefinedCallback(schema => refined.TrySetResult(schema));
        var invokeCount = 0;

        // Act
        var continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            action =>
            {
                Interlocked.Increment(ref invokeCount);
                action();
            },
            state.Cts.Token);
        var refinedSchema = await refined.Task.WaitAsync(TimeSpan.FromSeconds(10));
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
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        var scanner = new IncrementalSchemaScanner(_jsonlFilePath);
        var initialSchema = await scanner.InitialScanAsync();
        var queuedInvokes = new List<Action>();
        var posted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            action =>
            {
                queuedInvokes.Add(action);
                posted.TrySetResult();
            },
            state.Cts.Token);
        await posted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Replace the session after the UI post was queued but before it ran; the file
        // path stays the scanned one, as in a same-file reopen.
        state.RenewCtsWithCancel();
        var replacementSchema = new TableSchema
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
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        var scanner = new IncrementalSchemaScanner(_jsonlFilePath);
        var initialSchema = await scanner.InitialScanAsync();
        var cancelledToken = state.Cts.Token;
        state.RenewCtsWithCancel();
        var invokeCount = 0;

        // Act
        var continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            _ => Interlocked.Increment(ref invokeCount),
            cancelledToken);
        await continuation;

        // Assert
        Volatile.Read(ref invokeCount).Should().Be(0);
    }

    [Fact]
    public async Task Start_WithFaultedBackgroundScan_CompletesWithoutInvokingUiThreadInvoke()
    {
        // Arrange
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        var initialSchema = CreateIdSchema();
        var scanner = new GatedSchemaScanner(
            initialSchema,
            backgroundFailure: new InvalidOperationException("background scan failed"));
        var invokeCount = 0;

        // Act
        var continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            _ => Interlocked.Increment(ref invokeCount),
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
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        var initialSchema = CreateIdSchema();
        var scanner = new GatedSchemaScanner(initialSchema, backgroundCanceled: true);
        var invokeCount = 0;

        // Act
        var continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            _ => Interlocked.Increment(ref invokeCount),
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
        var initialSchema = CreateIdSchema();
        var scanner = new GatedSchemaScanner(initialSchema);

        // Act
        // A statement body keeps the lambda Action-returning, so Throw asserts a synchronous throw.
        var act = () =>
        {
            BackgroundSchemaRefiner.StartAsync(
                null!,
                scanner,
                initialSchema,
                _ => { },
                CancellationToken.None);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("state");
    }

    [Fact]
    public void Start_WithNullScanner_ThrowsArgumentNullException()
    {
        // Arrange
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        var initialSchema = CreateIdSchema();

        // Act
        // A statement body keeps the lambda Action-returning, so Throw asserts a synchronous throw.
        var act = () =>
        {
            BackgroundSchemaRefiner.StartAsync(
                state,
                null!,
                initialSchema,
                _ => { },
                state.Cts.Token);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("scanner");
    }

    [Fact]
    public void Start_WithNullInitialSchema_ThrowsArgumentNullException()
    {
        // Arrange
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        var scanner = new GatedSchemaScanner(CreateIdSchema());

        // Act
        // A statement body keeps the lambda Action-returning, so Throw asserts a synchronous throw.
        var act = () =>
        {
            BackgroundSchemaRefiner.StartAsync(
                state,
                scanner,
                null!,
                _ => { },
                state.Cts.Token);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("initialSchema");
    }

    [Fact]
    public void Start_WithNullUiThreadInvoke_ThrowsArgumentNullException()
    {
        // Arrange
        using var state = new AppState();
        state.StartNewFile(_jsonlFilePath);
        var initialSchema = CreateIdSchema();
        var scanner = new GatedSchemaScanner(initialSchema);

        // Act
        // A statement body keeps the lambda Action-returning, so Throw asserts a synchronous throw.
        var act = () =>
        {
            BackgroundSchemaRefiner.StartAsync(
                state,
                scanner,
                initialSchema,
                null!,
                state.Cts.Token);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("uiThreadInvoke");
    }

    /// <summary>
    /// Writes a JSON Lines fixture whose first 200 lines fit the initial scan (single
    /// "name" column) and whose remaining lines introduce an "age" column, so the
    /// background refinement must produce a schema that differs from the initial one.
    /// </summary>
    private async Task WriteRefinementFixtureAsync()
    {
        var initialLines = Enumerable.Range(0, 200).Select(i => $"{{\"name\":\"user{i}\"}}");
        var refinedLines = Enumerable.Range(0, 5).Select(i => $"{{\"name\":\"user{i}\",\"age\":{i}}}");
        var content = string.Join("\n", initialLines.Concat(refinedLines)) + "\n";
        await File.WriteAllTextAsync(_jsonlFilePath, content);
    }

    private static TableSchema CreateIdSchema() => new TableSchema
    {
        Columns = [new ColumnSchema { Name = "id", Type = ColumnType.WholeNumber }],
        SourceFormat = DataFormat.JsonLines
    };
}

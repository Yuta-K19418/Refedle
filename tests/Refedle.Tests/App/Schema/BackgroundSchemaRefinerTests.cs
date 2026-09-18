using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Schema;
using Refedle.App.Schema.JsonLines;

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
        using var state = new AppState { CurrentFilePath = _jsonlFilePath };
        var scanner = new IncrementalSchemaScanner(_jsonlFilePath);
        var initialSchema = await scanner.InitialScanAsync();
        var refined = new TaskCompletionSource<Refedle.Engine.Models.TableSchema>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        state.OnSchemaRefined = schema => refined.TrySetResult(schema);
        var invokeCount = 0;

        // Act
        var continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            _jsonlFilePath,
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
        using var state = new AppState { CurrentFilePath = _jsonlFilePath };
        var scanner = new IncrementalSchemaScanner(_jsonlFilePath);
        var initialSchema = await scanner.InitialScanAsync();
        var queuedInvokes = new List<Action>();
        var posted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var continuation = BackgroundSchemaRefiner.StartAsync(
            state,
            scanner,
            initialSchema,
            _jsonlFilePath,
            action =>
            {
                queuedInvokes.Add(action);
                posted.TrySetResult();
            },
            state.Cts.Token);
        await posted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Simulate loading a different file after the UI post was queued but before it ran.
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
        using var state = new AppState { CurrentFilePath = _jsonlFilePath };
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
            _jsonlFilePath,
            _ => Interlocked.Increment(ref invokeCount),
            cancelledToken);
        await continuation;

        // Assert
        Volatile.Read(ref invokeCount).Should().Be(0);
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
}

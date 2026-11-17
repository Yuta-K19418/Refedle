using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;
using Refedle.TestHelpers.App.Tui.Workers.Schema;

namespace Refedle.IntegrationTests.App.Tui.UI;

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
    public async Task ToggleJsonLinesModeAsync_BackgroundSchemaCompletes_RaisesSchemaRefinedOnUiThread()
    {
        // Arrange
        await WriteRefinementFixtureAsync(_jsonlFilePath);
        TaskCompletionSource<TableSchema> refined = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int callbackThreadId = 0;
        int uiThreadId = 0;
        await using LivePumpTestSession<ModeController> session = await LivePumpTestSession.StartAsync((app, _) =>
        {
            RowIndexer indexer = new(_jsonlFilePath);
            indexer.BuildIndex();
            AppState state = new();
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
        TableSchema refinedSchema = await refined.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        callbackThreadId.Should().Be(uiThreadId);
        refinedSchema.Columns.Select(c => c.Name).Should().Contain("age");
    }

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
}

using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;
using Refedle.TestHelpers.App.Tui.Workers.Schema;
using Refedle.TestHelpers.IO;
using static Refedle.TestHelpers.IO.RefinementFixtureFile;

namespace Refedle.IntegrationTests.App.Tui.UI;

public sealed class ModeControllerTests : IDisposable
{
    private readonly TempFile _tempFile = TempFile.ReservePath(".jsonl");

    private readonly string _jsonlFilePath;

    public ModeControllerTests()
    {
        _jsonlFilePath = _tempFile.Path;
    }

    public void Dispose() => _tempFile.Dispose();

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
}

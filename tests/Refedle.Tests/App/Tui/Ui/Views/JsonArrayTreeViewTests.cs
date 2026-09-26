using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views;
using Refedle.Engine.IO.JsonArray;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace Refedle.Tests.App.Tui.Ui.Views;

public sealed partial class JsonArrayTreeViewTests : IDisposable
{
    private readonly List<string> _tempFiles = [];
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            _disposed = true;
        }
    }

    private string CreateTempFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"jsonarray_treeview_{Guid.NewGuid()}.json");
        File.WriteAllText(filePath, content);
        _tempFiles.Add(filePath);
        return filePath;
    }

    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        Assert.NotNull(app.Driver);
        app.Driver.SetScreenSize(80, 25);
        return app;
    }

    private static void SynchronousUiThreadInvoke(Action action) => action();

    [Fact]
    public void HandleAccepted_NonRangeNode_DoesNotThrow()
    {
        // Arrange
        var filePath = CreateTempFile("[{\"a\":1}]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        using var view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var elementNode = objects.First();
        view.SelectedObject = elementNode;

        // Act — Accept on a non-range node should not throw
        var act = () => view.InvokeCommand(Command.Accept);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_UnsubscribesEventHandlers()
    {
        // Arrange — view created with event subscriptions via in-progress indexer
        var filePath = CreateTempFile("[1, 2]");
        using var app = CreateTestApp();
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        var stubIndexer = new StubRowIndexer(realIndexer, 0, fakeIsCompleted: false);
        using var view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Act — simulate progress, then dispose
        stubIndexer.UpdateTotalRows(3000);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);
        var objectsBefore = view.Objects;
        objectsBefore.Should().NotBeNull();
        var countBeforeDispose = objectsBefore.ToList().Count;
        view.Dispose();

        // Assert — raising events after disposal does not throw (handlers unsubscribed)
        countBeforeDispose.Should().Be(3);
        stubIndexer.UpdateTotalRows(6000);
        var act = () => stubIndexer.RaiseProgressChanged(0, 0);
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_FastPath_DoesNotThrow()
    {
        // Arrange — IsIndexingCompleted == true at creation (fast-path, no event subscriptions)
        var filePath = CreateTempFile("[1, 2, 3]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        using var view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Act & Assert — Dispose on fast-path view should not throw
        var act = () => view.Dispose();
        act.Should().NotThrow();
    }
}

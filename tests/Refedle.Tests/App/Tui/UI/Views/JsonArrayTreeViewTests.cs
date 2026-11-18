using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.IO.JsonArray;
using Refedle.TestHelpers.App.Tui.UI;
using Refedle.TestHelpers.IO;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using static Refedle.Tests.App.Tui.UI.Views.UiThreadInvokes;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed partial class JsonArrayTreeViewTests : IDisposable
{
    private readonly TempFileCollection _tempFiles = new(".json");

    public void Dispose() => _tempFiles.Dispose();

    [Fact]
    public void HandleAccepted_NonRangeNode_DoesNotThrow()
    {
        // Arrange
        string filePath = _tempFiles.Create("[{\"a\":1}]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        using JsonArrayTreeView view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        ITreeNode elementNode = objects.First();
        view.SelectedObject = elementNode;

        // Act — Accept on a non-range node should not throw
        Func<bool?> act = () => view.InvokeCommand(Command.Accept);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_UnsubscribesEventHandlers()
    {
        // Arrange — view created with event subscriptions via in-progress indexer
        string filePath = _tempFiles.Create("[1, 2]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0, fakeIsCompleted: false);
        using JsonArrayTreeView view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Act — simulate progress, then dispose
        stubIndexer.UpdateTotalRows(3000);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);
        IEnumerable<ITreeNode>? objectsBefore = view.Objects;
        objectsBefore.Should().NotBeNull();
        int countBeforeDispose = objectsBefore.ToList().Count;
        view.Dispose();

        // Assert — raising events after disposal does not throw (handlers unsubscribed)
        countBeforeDispose.Should().Be(3);
        stubIndexer.UpdateTotalRows(6000);
        Action act = () => stubIndexer.RaiseProgressChanged(0, 0);
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_FastPath_DoesNotThrow()
    {
        // Arrange — IsIndexingCompleted == true at creation (fast-path, no event subscriptions)
        string filePath = _tempFiles.Create("[1, 2, 3]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        using JsonArrayTreeView view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Act & Assert — Dispose on fast-path view should not throw
        Action act = () => view.Dispose();
        act.Should().NotThrow();
    }
}

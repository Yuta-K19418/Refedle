using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.UI.Views.JsonRangeTreeNodes;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonArray;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed partial class JsonArrayTreeViewTests
{
    [Fact]
    public void Create_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();

        // Act
        Func<JsonArrayTreeView> act = () => JsonArrayTreeView.Create(null!, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullOnTableModeToggle_ThrowsArgumentNullException()
    {
        // Arrange
        string filePath = CreateTempFile("[1]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        Func<JsonArrayTreeView> act = () => JsonArrayTreeView.Create(indexer, null!, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullUiThreadInvoke_ThrowsArgumentNullException()
    {
        // Arrange
        string filePath = CreateTempFile("[1]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        Func<JsonArrayTreeView> act = () => JsonArrayTreeView.Create(indexer, () => { }, _ => { }, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullOnPathChanged_ThrowsArgumentNullException()
    {
        // Arrange
        string filePath = CreateTempFile("[1]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        Func<JsonArrayTreeView> act = () => JsonArrayTreeView.Create(indexer, () => { }, null!, SynchronousUiThreadInvoke);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_OnSelectionChanged_InvokesOnPathChangedWithExpectedKeyPath()
    {
        // Arrange
        string filePath = CreateTempFile("[{\"a\":1}]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        List<IReadOnlyList<KeyPathSegment>> observedPaths = [];
        using JsonArrayTreeView view = JsonArrayTreeView.Create(
            indexer, () => { }, path => observedPaths.Add(path), SynchronousUiThreadInvoke);
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        ITreeNode elementNode = objects.First();
        ITreeNode childNode = elementNode.Children.First();

        // Act
        view.SelectedObject = childNode;

        // Assert
        observedPaths.Should().ContainSingle();
        observedPaths[0].Should().Equal(new KeyPathSegment("a", KeyPathSegmentKind.Key));
    }

    [Fact]
    public void Create_SmallArray_AddsElementNodesDirectly()
    {
        // Arrange
        string filePath = CreateTempFile("[1, 2, 3, 4, 5]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(5);
        list.Should().NotContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_ExactBoundary_AddsElementNodesDirectly()
    {
        // Arrange — 1000 elements is exactly the boundary, uses direct element nodes
        IEnumerable<string> elements = Enumerable.Range(0, 1000)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        string filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(1000);
        list.Should().NotContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_LargeArray_AddsRangeNodes()
    {
        // Arrange — 1001 elements triggers range mode
        IEnumerable<string> elements = Enumerable.Range(0, 1001)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        string filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(2);
        list.Should().OnlyContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_LargeArray_CorrectRangeCount()
    {
        // Arrange — 2500 elements → 3 ranges: [0-999], [1000-1999], [2000-2499]
        IEnumerable<string> elements = Enumerable.Range(0, 2500)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        string filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(3);
        list[0].Text.Should().Be("[0 - 999]");
        list[1].Text.Should().Be("[1,000 - 1,999]");
        list[2].Text.Should().Be("[2,000 - 2,499]");
    }

    [Fact]
    public void Create_EmptyArray_AddsNoNodes()
    {
        // Arrange
        string filePath = CreateTempFile("[]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().BeEmpty();
    }

    [Fact]
    public void Create_SmallArray_SkipsEmptyBytes_WhenReaderReturnsFewerElements()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        string filePath = CreateTempFile("[1, 2]");
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        // Stub says TotalRows=3 but file only has 2 elements → ReadElements returns only 2 elements
        StubRowIndexer stubIndexer = new(realIndexer, 3);

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — only 2 nodes added (Empty for index 2 is skipped)
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().HaveCount(2);
    }

    [Fact]
    public void Create_LargeArray_RangeNodesAreNotEagerlyLoaded()
    {
        // Arrange — 1001 elements triggers range mode
        IEnumerable<string> elements = Enumerable.Range(0, 1001)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        string filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — DelegateTreeBuilder prevents eager loading via AddObject()
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        objects.OfType<JsonArrayRangeTreeNode>().Should().OnlyContain(n => !n.IsChildrenLoaded);
    }

    [Fact]
    public void Create_IndexingCompleted_LargeArray_AddsRangeNodes()
    {
        // Arrange — indexer has already completed indexing with TotalRows > 1000
        IEnumerable<string> elements = Enumerable.Range(0, 2500)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        string filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — IsIndexingCompleted is true, TotalRows=2500, range nodes
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(3);
        list.Should().OnlyContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_IndexingCompleted_VeryLargeArray_AddsSuperRangeNodes()
    {
        // Arrange — indexer completed, file size suggests > 1M estimated rows
        string filePath = CreateTempFile("[1, 2]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        // Fake: 200MB file → estimatedRows=2,000,000 → superRangeSize=2,000; TotalRows=5000
        StubRowIndexer stubIndexer = new(realIndexer, 5000, fakeIsCompleted: true, fakeFileSize: 200_000_000);

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — 3 range nodes: [0-1999], [2000-3999], [4000-4999]
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(3);
        list.Should().OnlyContain(o => o is JsonArrayRangeTreeNode);
        list[0].Text.Should().Be("[0 - 1,999]");
        list[1].Text.Should().Be("[2,000 - 3,999]");
        list[2].Text.Should().Be("[4,000 - 4,999]");
    }

    [Fact]
    public void Create_IndexingInProgress_SubscribesToProgressChanged()
    {
        // Arrange — indexer is still building (IsIndexingCompleted == false)
        string filePath = CreateTempFile("[1, 2]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — no nodes yet (TotalRows = 0)
        IEnumerable<ITreeNode>? objectsEmpty = view.Objects;
        objectsEmpty.Should().NotBeNull();
        objectsEmpty.ToList().Should().BeEmpty();

        // Simulate progress — TotalRows increases to 3000
        stubIndexer.UpdateTotalRows(3000);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);

        // 3 range nodes added (nodeGroupSize=1000 for small file)
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(3);
        list.Should().OnlyContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_IndexingInProgress_TOCTOU_CompletedBeforeSubscribe()
    {
        // Arrange — indexer completes between FirstCheckpointReached and Create() call
        string filePath = CreateTempFile("[1, 2, 3]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        ToctouStubRowIndexer stubIndexer = new(realIndexer, 3);

        // Act — Create enters the in-progress branch (first check = false),
        // subscribes to events, then TOCTOU check finds completed → manual _completedHandler
        using JsonArrayTreeView view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — TOCTOU path uses AddNodesBatch which creates 1 range node (count=3)
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(1);
        list[0].Should().BeOfType<JsonArrayRangeTreeNode>();
    }

    [Fact]
    public void Create_IndexingInProgress_BuildIndexCompleted_AddsRemainderNode()
    {
        // Arrange — TotalRows=3500, nodeGroupSize=1000 (small file size)
        // ProgressChanged adds 3 full group nodes, BuildIndexCompleted adds 1 remainder node → 4 total
        string filePath = CreateTempFile("[1, 2]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Simulate progressive loading — 3500 elements indexed so far
        stubIndexer.UpdateTotalRows(3500);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);

        // Assert — 3 full group nodes added via ProgressChanged
        IEnumerable<ITreeNode>? objectsAfterProgress = view.Objects;
        objectsAfterProgress.Should().NotBeNull();
        List<ITreeNode> listAfterProgress = [.. objectsAfterProgress];
        listAfterProgress.Should().HaveCount(3);
        listAfterProgress[0].Text.Should().Be("[0 - 999]");
        listAfterProgress[1].Text.Should().Be("[1,000 - 1,999]");
        listAfterProgress[2].Text.Should().Be("[2,000 - 2,999]");

        // Simulate BuildIndexCompleted — remainder node added
        stubIndexer.RaiseBuildIndexCompleted();

        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(4);
        list[3].Text.Should().Be("[3,000 - 3,499]");
    }

    [Fact]
    public void Create_ProgressChanged_MultipleFires_DoNotDuplicateNodes()
    {
        // Arrange
        string filePath = CreateTempFile("[1, 2]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — successive progress fires advance the count without duplicating
        stubIndexer.UpdateTotalRows(3000);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);

        IEnumerable<ITreeNode>? first = view.Objects;
        first.Should().NotBeNull();
        first.ToList().Should().HaveCount(3);

        stubIndexer.UpdateTotalRows(5000);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);

        IEnumerable<ITreeNode>? second = view.Objects;
        second.Should().NotBeNull();
        second.ToList().Should().HaveCount(5);
        second.ToList().Should().OnlyContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_BuildIndexCompleted_MultipleFires_DoNotDuplicateRemainderNode()
    {
        // Arrange — TotalRows=3500, nodeGroupSize=1000 → 3 full + 1 remainder = 4
        string filePath = CreateTempFile("[1, 2]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        stubIndexer.UpdateTotalRows(3500);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);

        // Assert — second and third BuildIndexCompleted fires must not add duplicate remainders
        stubIndexer.RaiseBuildIndexCompleted();
        stubIndexer.RaiseBuildIndexCompleted();
        stubIndexer.RaiseBuildIndexCompleted();

        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(4);
        list.Count(o => o is JsonArrayRangeTreeNode r && r.Text == "[3,000 - 3,499]")
            .Should().Be(1);
    }

    [Fact]
    public void Create_Disposed_BuildIndexCompletedDoesNotThrowOrAddNodes()
    {
        // Arrange
        string filePath = CreateTempFile("[1, 2]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0, fakeIsCompleted: false);
        using JsonArrayTreeView view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        stubIndexer.UpdateTotalRows(3000);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);
        IEnumerable<ITreeNode>? objectsBeforeDispose = view.Objects;
        objectsBeforeDispose.Should().NotBeNull();
        int countBeforeDispose = objectsBeforeDispose.ToList().Count;
        view.Dispose();

        // Act — events raised after disposal
        stubIndexer.UpdateTotalRows(5000);
        Action act = () => stubIndexer.RaiseBuildIndexCompleted();

        // Assert — no throw, no new nodes (handlers unsubscribed on dispose)
        act.Should().NotThrow();
        IEnumerable<ITreeNode>? objectsAfter = view.Objects;
        objectsAfter.Should().NotBeNull();
        objectsAfter.ToList().Should().HaveCount(countBeforeDispose);
    }

    [Fact]
    public void Create_BuildIndexCompleted_ZeroRemainder_AddsNoExtraNode()
    {
        // Arrange — TotalRows=3000 is an exact multiple of nodeGroupSize=1000 → remainder=0
        string filePath = CreateTempFile("[1, 2]");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using JsonArrayTreeView view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        stubIndexer.UpdateTotalRows(3000);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);
        stubIndexer.RaiseBuildIndexCompleted();

        // Assert — exactly 3 full group nodes, no extra remainder node
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().HaveCount(3);
        objects.ToList().Should().OnlyContain(o => o is JsonArrayRangeTreeNode);
    }
}

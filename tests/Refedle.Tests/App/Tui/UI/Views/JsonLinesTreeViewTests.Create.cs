using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.UI.Views.JsonRangeTreeNodes;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonLines;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed partial class JsonLinesTreeViewTests
{
    [Fact]
    public void Create_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();

        // Act
        Func<JsonLinesTreeView> act = () => JsonLinesTreeView.Create(null!, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullOnTableModeToggle_ThrowsArgumentNullException()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        string filePath = _tempFiles.Create("{\"a\":1}");
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        Func<JsonLinesTreeView> act = () => JsonLinesTreeView.Create(indexer, null!, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullUiThreadInvoke_ThrowsArgumentNullException()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        string filePath = _tempFiles.Create("{\"a\":1}");
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        Func<JsonLinesTreeView> act = () => JsonLinesTreeView.Create(indexer, () => { }, _ => { }, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullOnPathChanged_ThrowsArgumentNullException()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        string filePath = _tempFiles.Create("{\"a\":1}");
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        Func<JsonLinesTreeView> act = () => JsonLinesTreeView.Create(indexer, () => { }, null!, SynchronousUiThreadInvoke);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_OnSelectionChanged_InvokesOnPathChangedWithExpectedKeyPath()
    {
        // Arrange
        string filePath = _tempFiles.Create("{\"a\":1}");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();
        List<IReadOnlyList<KeyPathSegment>> observedPaths = [];
        using JsonLinesTreeView view = JsonLinesTreeView.Create(
            indexer, () => { }, path => observedPaths.Add(path), SynchronousUiThreadInvoke);
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        ITreeNode lineNode = objects.First();
        ITreeNode childNode = lineNode.Children.First();

        // Act
        view.SelectedObject = childNode;

        // Assert
        observedPaths.Should().ContainSingle();
        observedPaths[0].Should().Equal(new KeyPathSegment("a", KeyPathSegmentKind.Key));
    }

    [Fact]
    public void Create_SmallFile_AddsLineNodesDirectly()
    {
        // Arrange
        string filePath = _tempFiles.Create("{\"a\":1}\n{\"b\":2}\n{\"c\":3}");
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(3);
        list.Should().NotContain(o => o is JsonLinesRangeTreeNode);
    }

    [Fact]
    public void Create_ExactBoundary_AddsLineNodesDirectly()
    {
        // Arrange — 1000 lines is exactly the boundary, uses direct line nodes
        IEnumerable<string> lines = Enumerable.Range(0, 1000)
            .Select(i => $"{{\"id\":{i}}}");
        string filePath = _tempFiles.Create(string.Join("\n", lines));
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(1000);
        list.Should().NotContain(o => o is JsonLinesRangeTreeNode);
    }

    [Fact]
    public void Create_LargeFile_AddsRangeNodes()
    {
        // Arrange — 1001 lines triggers range mode
        IEnumerable<string> lines = Enumerable.Range(0, 1001)
            .Select(i => $"{{\"id\":{i}}}");
        string filePath = _tempFiles.Create(string.Join("\n", lines));
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(2);
        list.Should().OnlyContain(o => o is JsonLinesRangeTreeNode);
    }

    [Fact]
    public void Create_LargeFile_CorrectRangeCount()
    {
        // Arrange — 2500 lines → 3 ranges: [0-999], [1000-1999], [2000-2499]
        IEnumerable<string> lines = Enumerable.Range(0, 2500)
            .Select(i => $"{{\"id\":{i}}}");
        string filePath = _tempFiles.Create(string.Join("\n", lines));
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(3);
        list[0].Text.Should().Be("Lines 1 - 1,000");
        list[1].Text.Should().Be("Lines 1,001 - 2,000");
        list[2].Text.Should().Be("Lines 2,001 - 2,500");
    }

    [Fact]
    public void Create_EmptyFile_AddsNoNodes()
    {
        // Arrange — non-empty file so RowReader can mmap, but stub reports 0 rows
        using IApplication app = TestAppFactory.Create();
        string filePath = _tempFiles.Create(" \n");
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0);

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().BeEmpty();
    }

    [Fact]
    public void Create_SmallFile_SkipsEmptyBytes_WhenCacheReturnsEmpty()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        string filePath = _tempFiles.Create("{\"a\":1}\n{\"b\":2}");
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        // Stub says TotalRows=3 but file only has 2 lines → GetRow(2) returns Empty
        StubRowIndexer stubIndexer = new(realIndexer, 3);

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — only 2 nodes added (Empty for index 2 is skipped)
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().HaveCount(2);
    }

    [Fact]
    public void Create_IndexingCompleted_LargeFile_AddsRangeNodes()
    {
        // Arrange — indexer has already completed indexing with TotalRows > 1000
        IEnumerable<string> lines = Enumerable.Range(0, 2500)
            .Select(i => $"{{\"id\":{i}}}");
        string filePath = _tempFiles.Create(string.Join("\n", lines));
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — IsIndexingCompleted is true, TotalRows=2500, range nodes
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(3);
        list.Should().OnlyContain(o => o is JsonLinesRangeTreeNode);
    }

    [Fact]
    public void Create_IndexingCompleted_VeryLargeFile_AddsSuperRangeNodes()
    {
        // Arrange — indexer completed, file size suggests > 1M estimated rows
        string filePath = _tempFiles.Create("{\"a\":1}\n{\"b\":2}");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        // Fake: 200MB file → estimatedRows=2,000,000 → superRangeSize=2,000; TotalRows=5000
        StubRowIndexer stubIndexer = new(realIndexer, 5000, fakeIsCompleted: true, fakeFileSize: 200_000_000);

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — 3 range nodes: [1-2000], [2001-4000], [4001-5000]
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(3);
        list.Should().OnlyContain(o => o is JsonLinesRangeTreeNode);
        list[0].Text.Should().Be("Lines 1 - 2,000");
        list[1].Text.Should().Be("Lines 2,001 - 4,000");
        list[2].Text.Should().Be("Lines 4,001 - 5,000");
    }

    [Fact]
    public void Create_IndexingInProgress_SubscribesToProgressChanged()
    {
        // Arrange — indexer is still building (IsIndexingCompleted == false)
        string filePath = _tempFiles.Create("{\"a\":1}\n{\"b\":2}");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

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
        list.Should().OnlyContain(o => o is JsonLinesRangeTreeNode);
    }

    [Fact]
    public void Create_LargeFile_RangeNodesAreNotEagerlyLoaded()
    {
        // Arrange — 1001 lines triggers range mode
        IEnumerable<string> lines = Enumerable.Range(0, 1001)
            .Select(i => $"{{\"id\":{i}}}");
        string filePath = _tempFiles.Create(string.Join("\n", lines));
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(filePath);
        indexer.BuildIndex();

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — DelegateTreeBuilder prevents eager loading via AddObject()
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        objects.OfType<JsonLinesRangeTreeNode>().Should().OnlyContain(n => !n.IsChildrenLoaded);
    }

    [Fact]
    public void Create_IndexingInProgress_TOCTOU_CompletedBeforeSubscribe()
    {
        // Arrange — indexer completes between FirstCheckpointReached and Create() call
        // ToctouStubRowIndexer returns false on first IsIndexingCompleted check,
        // then true on the TOCTOU re-check inside Create
        string filePath = _tempFiles.Create("{\"a\":1}\n{\"b\":2}\n{\"c\":3}");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        ToctouStubRowIndexer stubIndexer = new(realIndexer, 3);

        // Act — Create enters the in-progress branch (first check = false),
        // subscribes to events, then TOCTOU check finds completed → manual _completedHandler
        using JsonLinesTreeView view = JsonLinesTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — TOCTOU path uses AddNodesBatch which creates 1 range node (count=3)
        // because totalRows=3 < nodeGroupSize=1000 → remainder node only
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(1);
        list[0].Should().BeOfType<JsonLinesRangeTreeNode>();
    }

    [Fact]
    public void Create_IndexingInProgress_BuildIndexCompleted_AddsRemainderNode()
    {
        // Arrange — TotalRows=3500, nodeGroupSize=1000 (small file size)
        // ProgressChanged adds 3 full group nodes, BuildIndexCompleted adds 1 remainder node → 4 total
        string filePath = _tempFiles.Create("{\"a\":1}\n{\"b\":2}");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Simulate progressive loading — 3500 rows indexed so far
        stubIndexer.UpdateTotalRows(3500);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);

        // Assert — 3 full group nodes added via ProgressChanged
        IEnumerable<ITreeNode>? objectsAfterProgress = view.Objects;
        objectsAfterProgress.Should().NotBeNull();
        List<ITreeNode> listAfterProgress = [.. objectsAfterProgress];
        listAfterProgress.Should().HaveCount(3);
        listAfterProgress[0].Text.Should().Be("Lines 1 - 1,000");
        listAfterProgress[1].Text.Should().Be("Lines 1,001 - 2,000");
        listAfterProgress[2].Text.Should().Be("Lines 2,001 - 3,000");

        // Simulate BuildIndexCompleted — remainder node added
        stubIndexer.RaiseBuildIndexCompleted();

        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(4);
        list[3].Text.Should().Be("Lines 3,001 - 3,500");
    }

    [Fact]
    public void Create_ProgressChanged_MultipleFires_DoNotDuplicateNodes()
    {
        // Arrange
        string filePath = _tempFiles.Create("{\"a\":1}\n{\"b\":2}");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

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
        second.ToList().Should().OnlyContain(o => o is JsonLinesRangeTreeNode);
    }

    [Fact]
    public void Create_BuildIndexCompleted_MultipleFires_DoNotDuplicateRemainderNode()
    {
        // Arrange — TotalRows=3500, nodeGroupSize=1000 → 3 full + 1 remainder = 4
        string filePath = _tempFiles.Create("{\"a\":1}\n{\"b\":2}");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

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
        list.Count(o => o is JsonLinesRangeTreeNode r && r.Text == "Lines 3,001 - 3,500")
            .Should().Be(1);
    }

    [Fact]
    public void Create_Disposed_BuildIndexCompletedDoesNotThrowOrAddNodes()
    {
        // Arrange
        string filePath = _tempFiles.Create("{\"a\":1}\n{\"b\":2}");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0, fakeIsCompleted: false);
        using JsonLinesTreeView view = JsonLinesTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

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
        string filePath = _tempFiles.Create("{\"a\":1}\n{\"b\":2}");
        using IApplication app = TestAppFactory.Create();
        RowIndexer realIndexer = new(filePath);
        realIndexer.BuildIndex();
        StubRowIndexer stubIndexer = new(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using JsonLinesTreeView view = JsonLinesTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        stubIndexer.UpdateTotalRows(3000);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);
        stubIndexer.RaiseBuildIndexCompleted();

        // Assert — exactly 3 full group nodes, no extra remainder node
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().HaveCount(3);
        objects.ToList().Should().OnlyContain(o => o is JsonLinesRangeTreeNode);
    }
}

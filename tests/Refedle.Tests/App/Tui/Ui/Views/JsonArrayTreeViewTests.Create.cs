using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views;
using Refedle.App.Tui.Ui.Views.JsonRangeTreeNodes;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonArray;

namespace Refedle.Tests.App.Tui.Ui.Views;

public sealed partial class JsonArrayTreeViewTests
{
    [Fact]
    public void Create_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        using var app = CreateTestApp();

        // Act
        var act = () => JsonArrayTreeView.Create(null!, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullOnTableModeToggle_ThrowsArgumentNullException()
    {
        // Arrange
        var filePath = CreateTempFile("[1]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        var act = () => JsonArrayTreeView.Create(indexer, null!, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullUiThreadInvoke_ThrowsArgumentNullException()
    {
        // Arrange
        var filePath = CreateTempFile("[1]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        var act = () => JsonArrayTreeView.Create(indexer, () => { }, _ => { }, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullOnPathChanged_ThrowsArgumentNullException()
    {
        // Arrange
        var filePath = CreateTempFile("[1]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        var act = () => JsonArrayTreeView.Create(indexer, () => { }, null!, SynchronousUiThreadInvoke);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_OnSelectionChanged_InvokesOnPathChangedWithExpectedKeyPath()
    {
        // Arrange
        var filePath = CreateTempFile("[{\"a\":1}]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        List<IReadOnlyList<KeyPathSegment>> observedPaths = [];
        using var view = JsonArrayTreeView.Create(
            indexer, () => { }, path => observedPaths.Add(path), SynchronousUiThreadInvoke);
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var elementNode = objects.First();
        var childNode = elementNode.Children.First();

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
        var filePath = CreateTempFile("[1, 2, 3, 4, 5]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(5);
        list.Should().NotContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_ExactBoundary_AddsElementNodesDirectly()
    {
        // Arrange — 1000 elements is exactly the boundary, uses direct element nodes
        var elements = Enumerable.Range(0, 1000)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(1000);
        list.Should().NotContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_LargeArray_AddsRangeNodes()
    {
        // Arrange — 1001 elements triggers range mode
        var elements = Enumerable.Range(0, 1001)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(2);
        list.Should().OnlyContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_LargeArray_CorrectRangeCount()
    {
        // Arrange — 2500 elements → 3 ranges: [0-999], [1000-1999], [2000-2499]
        var elements = Enumerable.Range(0, 2500)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(3);
        list[0].Text.Should().Be("[0 - 999]");
        list[1].Text.Should().Be("[1,000 - 1,999]");
        list[2].Text.Should().Be("[2,000 - 2,499]");
    }

    [Fact]
    public void Create_EmptyArray_AddsNoNodes()
    {
        // Arrange
        var filePath = CreateTempFile("[]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().BeEmpty();
    }

    [Fact]
    public void Create_SmallArray_SkipsEmptyBytes_WhenReaderReturnsFewerElements()
    {
        // Arrange
        using var app = CreateTestApp();
        var filePath = CreateTempFile("[1, 2]");
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        // Stub says TotalRows=3 but file only has 2 elements → ReadElements returns only 2 elements
        var stubIndexer = new StubRowIndexer(realIndexer, 3);

        // Act
        using var view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — only 2 nodes added (Empty for index 2 is skipped)
        var objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().HaveCount(2);
    }

    [Fact]
    public void Create_LargeArray_RangeNodesAreNotEagerlyLoaded()
    {
        // Arrange — 1001 elements triggers range mode
        var elements = Enumerable.Range(0, 1001)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — DelegateTreeBuilder prevents eager loading via AddObject()
        var objects = view.Objects;
        objects.Should().NotBeNull();
        objects.OfType<JsonArrayRangeTreeNode>().Should().OnlyContain(n => !n.IsChildrenLoaded);
    }

    [Fact]
    public void Create_IndexingCompleted_LargeArray_AddsRangeNodes()
    {
        // Arrange — indexer has already completed indexing with TotalRows > 1000
        var elements = Enumerable.Range(0, 2500)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonArrayTreeView.Create(indexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — IsIndexingCompleted is true, TotalRows=2500, range nodes
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(3);
        list.Should().OnlyContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_IndexingCompleted_VeryLargeArray_AddsSuperRangeNodes()
    {
        // Arrange — indexer completed, file size suggests > 1M estimated rows
        var filePath = CreateTempFile("[1, 2]");
        using var app = CreateTestApp();
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        // Fake: 200MB file → estimatedRows=2,000,000 → superRangeSize=2,000; TotalRows=5000
        var stubIndexer = new StubRowIndexer(realIndexer, 5000, fakeIsCompleted: true, fakeFileSize: 200_000_000);

        // Act
        using var view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — 3 range nodes: [0-1999], [2000-3999], [4000-4999]
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
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
        var filePath = CreateTempFile("[1, 2]");
        using var app = CreateTestApp();
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        var stubIndexer = new StubRowIndexer(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using var view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — no nodes yet (TotalRows = 0)
        var objectsEmpty = view.Objects;
        objectsEmpty.Should().NotBeNull();
        objectsEmpty.ToList().Should().BeEmpty();

        // Simulate progress — TotalRows increases to 3000
        stubIndexer.UpdateTotalRows(3000);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);

        // 3 range nodes added (nodeGroupSize=1000 for small file)
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(3);
        list.Should().OnlyContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_IndexingInProgress_TOCTOU_CompletedBeforeSubscribe()
    {
        // Arrange — indexer completes between FirstCheckpointReached and Create() call
        var filePath = CreateTempFile("[1, 2, 3]");
        using var app = CreateTestApp();
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        var stubIndexer = new ToctouStubRowIndexer(realIndexer, 3);

        // Act — Create enters the in-progress branch (first check = false),
        // subscribes to events, then TOCTOU check finds completed → manual _completedHandler
        using var view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — TOCTOU path uses AddNodesBatch which creates 1 range node (count=3)
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(1);
        list[0].Should().BeOfType<JsonArrayRangeTreeNode>();
    }

    [Fact]
    public void Create_IndexingInProgress_BuildIndexCompleted_AddsRemainderNode()
    {
        // Arrange — TotalRows=3500, nodeGroupSize=1000 (small file size)
        // ProgressChanged adds 3 full group nodes, BuildIndexCompleted adds 1 remainder node → 4 total
        var filePath = CreateTempFile("[1, 2]");
        using var app = CreateTestApp();
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        var stubIndexer = new StubRowIndexer(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using var view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Simulate progressive loading — 3500 elements indexed so far
        stubIndexer.UpdateTotalRows(3500);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);

        // Assert — 3 full group nodes added via ProgressChanged
        var objectsAfterProgress = view.Objects;
        objectsAfterProgress.Should().NotBeNull();
        var listAfterProgress = objectsAfterProgress.ToList();
        listAfterProgress.Should().HaveCount(3);
        listAfterProgress[0].Text.Should().Be("[0 - 999]");
        listAfterProgress[1].Text.Should().Be("[1,000 - 1,999]");
        listAfterProgress[2].Text.Should().Be("[2,000 - 2,999]");

        // Simulate BuildIndexCompleted — remainder node added
        stubIndexer.RaiseBuildIndexCompleted();

        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(4);
        list[3].Text.Should().Be("[3,000 - 3,499]");
    }

    [Fact]
    public void Create_ProgressChanged_MultipleFires_DoNotDuplicateNodes()
    {
        // Arrange
        var filePath = CreateTempFile("[1, 2]");
        using var app = CreateTestApp();
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        var stubIndexer = new StubRowIndexer(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using var view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        // Assert — successive progress fires advance the count without duplicating
        stubIndexer.UpdateTotalRows(3000);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);

        var first = view.Objects;
        first.Should().NotBeNull();
        first.ToList().Should().HaveCount(3);

        stubIndexer.UpdateTotalRows(5000);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);

        var second = view.Objects;
        second.Should().NotBeNull();
        second.ToList().Should().HaveCount(5);
        second.ToList().Should().OnlyContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_BuildIndexCompleted_MultipleFires_DoNotDuplicateRemainderNode()
    {
        // Arrange — TotalRows=3500, nodeGroupSize=1000 → 3 full + 1 remainder = 4
        var filePath = CreateTempFile("[1, 2]");
        using var app = CreateTestApp();
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        var stubIndexer = new StubRowIndexer(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using var view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        stubIndexer.UpdateTotalRows(3500);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);

        // Assert — second and third BuildIndexCompleted fires must not add duplicate remainders
        stubIndexer.RaiseBuildIndexCompleted();
        stubIndexer.RaiseBuildIndexCompleted();
        stubIndexer.RaiseBuildIndexCompleted();

        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(4);
        list.Count(o => o is JsonArrayRangeTreeNode r && r.Text == "[3,000 - 3,499]")
            .Should().Be(1);
    }

    [Fact]
    public void Create_Disposed_BuildIndexCompletedDoesNotThrowOrAddNodes()
    {
        // Arrange
        var filePath = CreateTempFile("[1, 2]");
        using var app = CreateTestApp();
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        var stubIndexer = new StubRowIndexer(realIndexer, 0, fakeIsCompleted: false);
        using var view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        stubIndexer.UpdateTotalRows(3000);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);
        var objectsBeforeDispose = view.Objects;
        objectsBeforeDispose.Should().NotBeNull();
        var countBeforeDispose = objectsBeforeDispose.ToList().Count;
        view.Dispose();

        // Act — events raised after disposal
        stubIndexer.UpdateTotalRows(5000);
        var act = () => stubIndexer.RaiseBuildIndexCompleted();

        // Assert — no throw, no new nodes (handlers unsubscribed on dispose)
        act.Should().NotThrow();
        var objectsAfter = view.Objects;
        objectsAfter.Should().NotBeNull();
        objectsAfter.ToList().Should().HaveCount(countBeforeDispose);
    }

    [Fact]
    public void Create_BuildIndexCompleted_ZeroRemainder_AddsNoExtraNode()
    {
        // Arrange — TotalRows=3000 is an exact multiple of nodeGroupSize=1000 → remainder=0
        var filePath = CreateTempFile("[1, 2]");
        using var app = CreateTestApp();
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        var stubIndexer = new StubRowIndexer(realIndexer, 0, fakeIsCompleted: false);

        // Act
        using var view = JsonArrayTreeView.Create(stubIndexer, () => { }, _ => { }, SynchronousUiThreadInvoke);

        stubIndexer.UpdateTotalRows(3000);
        stubIndexer.RaiseProgressChanged(0, stubIndexer.FileSize);
        stubIndexer.RaiseBuildIndexCompleted();

        // Assert — exactly 3 full group nodes, no extra remainder node
        var objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().HaveCount(3);
        objects.ToList().Should().OnlyContain(o => o is JsonArrayRangeTreeNode);
    }
}

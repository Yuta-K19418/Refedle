using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.UI.Views.JsonRangeTreeNodes;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonObject;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed partial class JsonObjectTreeViewTests
{
    [Fact]
    public void Create_WithNullEntries_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<JsonObjectEntry> nullEntries = null!;

        // Act
        Func<JsonObjectTreeView> act = () => JsonObjectTreeView.Create(nullEntries, () => { }, _ => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullToggle_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<JsonObjectEntry> entries = [];

        // Act
        Func<JsonObjectTreeView> act = () => JsonObjectTreeView.Create(entries, null!, _ => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullOnPathChanged_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<JsonObjectEntry> entries = [];

        // Act
        Func<JsonObjectTreeView> act = () => JsonObjectTreeView.Create(entries, () => { }, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithEmptyEntries_AddsNoObjects()
    {
        // Arrange
        IReadOnlyList<JsonObjectEntry> entries = [];

        // Act
        using JsonObjectTreeView view = JsonObjectTreeView.Create(entries, () => { }, _ => { });

        // Assert
        view.Objects.Should().BeEmpty();
    }

    [Fact]
    public void Create_OnSelectionChanged_InvokesOnPathChangedWithExpectedKeyPath()
    {
        // Arrange
        IReadOnlyList<JsonObjectEntry> entries = [new JsonObjectEntry("data", ToBytes("{\"a\":1}"))];
        List<IReadOnlyList<KeyPathSegment>> observedPaths = [];
        using JsonObjectTreeView view = JsonObjectTreeView.Create(entries, () => { }, path => observedPaths.Add(path));
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        ITreeNode node = objects.First();

        // Act
        view.SelectedObject = node;

        // Assert
        observedPaths.Should().ContainSingle();
        observedPaths[0].Should().Equal(new KeyPathSegment("data", KeyPathSegmentKind.Key));
    }

    [Fact]
    public void Create_WithEntries_AddsOneNodePerKey()
    {
        // Arrange
        IReadOnlyList<JsonObjectEntry> entries =
        [
            new JsonObjectEntry("id", ToBytes("1")),
            new JsonObjectEntry("name", ToBytes("\"Alice\"")),
            new JsonObjectEntry("active", ToBytes("true")),
        ];

        // Act
        using JsonObjectTreeView view = JsonObjectTreeView.Create(entries, () => { }, _ => { });

        // Assert
        view.Objects.Should().HaveCount(3);
    }

    [Fact]
    public void Create_EntryCountAtBoundary_AddsKeyNodesDirectly()
    {
        // Arrange — 1000 entries is exactly the boundary, keys go directly under the root
        List<JsonObjectEntry> entries = CreateNumberedEntries(1000);

        // Act
        using JsonObjectTreeView view = JsonObjectTreeView.Create(entries, () => { }, _ => { });

        // Assert
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(1000);
        list.Should().NotContain(o => o is JsonObjectRangeTreeNode);
    }

    [Fact]
    public void Create_EntryCountAboveBoundary_AddsRangeNodes()
    {
        // Arrange — 1001 entries triggers range mode
        List<JsonObjectEntry> entries = CreateNumberedEntries(1001);

        // Act
        using JsonObjectTreeView view = JsonObjectTreeView.Create(entries, () => { }, _ => { });

        // Assert
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<ITreeNode> list = [.. objects];
        list.Should().HaveCount(2);
        list.Should().OnlyContain(o => o is JsonObjectRangeTreeNode);
    }

    [Fact]
    public void Create_LargeEntryCount_CorrectRangeCount()
    {
        // Arrange — 2500 entries → 3 ranges: [0-999], [1000-1999], [2000-2499]
        List<JsonObjectEntry> entries = CreateNumberedEntries(2500);

        // Act
        using JsonObjectTreeView view = JsonObjectTreeView.Create(entries, () => { }, _ => { });

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
    public void Create_LargeEntryCount_RangeNodesAreNotEagerlyLoaded()
    {
        // Arrange
        List<JsonObjectEntry> entries = CreateNumberedEntries(1001);

        // Act
        using JsonObjectTreeView view = JsonObjectTreeView.Create(entries, () => { }, _ => { });

        // Assert — DelegateTreeBuilder prevents eager loading via AddObject()
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        objects.OfType<JsonObjectRangeTreeNode>().Should().OnlyContain(n => !n.IsChildrenLoaded);
    }

    [Fact]
    public void Create_LargeEntryCount_ExpandedLastRangeShowsRemainderKeysAndValues()
    {
        // Arrange — 2500 entries; last range [2,000 - 2,499] holds the 500-entry remainder
        List<JsonObjectEntry> entries = CreateNumberedEntries(2500);
        using JsonObjectTreeView view = JsonObjectTreeView.Create(entries, () => { }, _ => { });
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        JsonObjectRangeTreeNode lastRange = objects.OfType<JsonObjectRangeTreeNode>().Last();

        // Act
        lastRange.EnsureChildrenLoaded();
        IList<ITreeNode> children = lastRange.Children;

        // Assert
        children.Should().HaveCount(500);
        children[0].Text.Should().Be("k2000: 2000");
        children[499].Text.Should().Be("k2499: 2499");
    }

    [Fact]
    public void Create_OnSelectionChanged_KeyNodeUnderRangeNode_InvokesOnPathChangedWithExpectedKeyPath()
    {
        // Arrange — a key node reached through a range node keeps the same KeyPath as a root-level one.
        // The last key holds an object because primitive key nodes carry no KeyName.
        List<JsonObjectEntry> entries = [.. CreateNumberedEntries(1000), new("k1000", ToBytes("{\"a\":1}"))];
        List<IReadOnlyList<KeyPathSegment>> observedPaths = [];
        using JsonObjectTreeView view = JsonObjectTreeView.Create(entries, () => { }, path => observedPaths.Add(path));
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        JsonObjectRangeTreeNode lastRange = objects.OfType<JsonObjectRangeTreeNode>().Last();
        lastRange.EnsureChildrenLoaded();
        ITreeNode keyNode = lastRange.Children[0];

        // Act
        view.SelectedObject = keyNode;

        // Assert
        observedPaths.Should().ContainSingle();
        observedPaths[0].Should().Equal(new KeyPathSegment("k1000", KeyPathSegmentKind.Key));
    }

    [Fact]
    public void Create_WithMoreThanRangeSizeEntries_TreeBuilderExpandsRangeNodeAndReturnsItsKeys()
    {
        // Arrange
        List<JsonObjectEntry> entries = CreateNumberedEntries(1001);
        using JsonObjectTreeView view = JsonObjectTreeView.Create(entries, () => { }, _ => { });
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        List<JsonObjectRangeTreeNode> ranges = [.. objects.OfType<JsonObjectRangeTreeNode>()];
        ITreeBuilder<ITreeNode>? treeBuilder = view.TreeBuilder;
        treeBuilder.Should().NotBeNull();

        // Act
        bool canExpand = treeBuilder.CanExpand(ranges[0]);
        List<ITreeNode> children = [.. treeBuilder.GetChildren(ranges[0])];
        List<ITreeNode> lastChildren = [.. treeBuilder.GetChildren(ranges[1])];

        // Assert
        canExpand.Should().BeTrue();
        children.Should().HaveCount(1000);
        children[0].Text.Should().Be("k0: 0");
        children[999].Text.Should().Be("k999: 999");
        lastChildren.Should().ContainSingle().Which.Text.Should().Be("k1000: 1000");
    }

    [Fact]
    public void Create_TreeBuilderExpandsObjectKeyNodeUnderRangeNode_ReturnsItsProperties()
    {
        // Arrange — the last range holds an object-valued key, as a real large file would
        List<JsonObjectEntry> entries = [.. CreateNumberedEntries(1000), new("k1000", ToBytes("{\"a\":1}"))];
        using JsonObjectTreeView view = JsonObjectTreeView.Create(entries, () => { }, _ => { });
        IEnumerable<ITreeNode>? objects = view.Objects;
        objects.Should().NotBeNull();
        JsonObjectRangeTreeNode lastRange = objects.OfType<JsonObjectRangeTreeNode>().Last();
        ITreeBuilder<ITreeNode>? treeBuilder = view.TreeBuilder;
        treeBuilder.Should().NotBeNull();
        ITreeNode keyNode = treeBuilder.GetChildren(lastRange).Single();

        // Act
        bool canExpand = treeBuilder.CanExpand(keyNode);
        List<ITreeNode> children = [.. treeBuilder.GetChildren(keyNode)];

        // Assert
        keyNode.Text.Should().Be("k1000: {Object: 1 properties}");
        canExpand.Should().BeTrue();
        children.Should().ContainSingle().Which.Text.Should().Be("a: 1");
    }
}

using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.JsonRangeTreeNodes;
using Refedle.App.Tui.UI.Views.JsonTreeNodes;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views.JsonRangeTreeNodes;

public sealed class RangeTreeNodeBaseTests
{
    [Fact]
    public void EnsureChildrenLoaded_OnSuccess_IsIdempotent()
    {
        // Arrange
        var node = new StubRangeNode(4);

        // Act
        node.EnsureChildrenLoaded();
        node.EnsureChildrenLoaded();

        // Assert — LoadChildren runs once; second call is a no-op
        node.IsChildrenLoaded.Should().BeTrue();
        node.Children.Should().HaveCount(4);
    }

    private sealed class StubRangeNode : RangeTreeNodeBase
    {
        internal StubRangeNode(int count) : base(0, count)
        {
            Text = "stub";
        }

        protected override RangeTreeNodeBase CreateRangeNode(long startIndex, long count) =>
            new StubRangeNode((int)count);

        protected override void AddDirectChildren()
        {
            List<ITreeNode> children = [];

            for (var i = 0; i < Count; i++)
            {
                children.Add(new JsonValueTreeNode($"child {i}"));
            }

            Children = children;
        }
    }
}

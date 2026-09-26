using AwesomeAssertions;
using Refedle.App.Tui.Ui;
using Refedle.App.Tui.Ui.Views.JsonTreeNodes;
using Refedle.Engine.IO.DrillDown;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.Ui;

public sealed class KeyPathBuilderTests
{
    [Fact]
    public void Build_WithRootSelection_ReturnsEmptyKeyPath()
    {
        // Arrange
        ITreeNode rootNode = new JsonValueTreeNode("root");

        // Act
        var keyPath = KeyPathBuilder.Build(rootNode);

        // Assert
        keyPath.Should().BeEmpty();
    }

    [Fact]
    public void Build_WithNestedObjectArraySelection_ReturnsOrderedSegmentsWithIndex()
    {
        // Arrange
        var root = new JsonObjectTreeNode("{}"u8.ToArray());
        var ordersArray = new JsonArrayTreeNode("[]"u8.ToArray()) { KeyName = "orders", ParentNode = root };
        var element0 = new JsonObjectTreeNode("{}"u8.ToArray()) { KeyName = "[0]", ParentNode = ordersArray };

        // Act
        var keyPath = KeyPathBuilder.Build(element0);

        // Assert
        keyPath.Should().Equal(
            new KeyPathSegment("orders", KeyPathSegmentKind.Key),
            new KeyPathSegment("[0]", KeyPathSegmentKind.Index));
    }
}

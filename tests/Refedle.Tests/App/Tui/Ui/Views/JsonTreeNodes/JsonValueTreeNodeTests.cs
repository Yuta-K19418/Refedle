using System.Text.Json;
using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views.JsonTreeNodes;

namespace Refedle.Tests.App.Tui.Ui.Views.JsonTreeNodes;

/// <summary>
/// Tests for the <see cref="JsonValueTreeNode"/> class.
/// </summary>
public sealed class JsonValueTreeNodeTests
{
    [Fact]
    public void Constructor_WithValidText_SetsTextProperty()
    {
        // Arrange
        var expectedText = "Test Value";

        // Act
        var node = new JsonValueTreeNode(expectedText);

        // Assert
        node.Text.Should().Be(expectedText);
    }

    [Fact]
    public void Constructor_WhenCalled_InitializesChildrenAsEmptyList()
    {
        // Arrange
        var node = new JsonValueTreeNode("Test");

        // Act
        var children = node.Children;

        // Assert
        children.Should().NotBeNull();
        children.Should().BeEmpty();
    }

    [Fact]
    public void ValueKind_WhenInitialized_StoresCorrectValue()
    {
        // Arrange
        var expectedKind = JsonValueKind.String;

        // Act
        var node = new JsonValueTreeNode("Test") { ValueKind = expectedKind };

        // Assert
        node.ValueKind.Should().Be(expectedKind);
    }
}

using System.Text.Json;
using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.JsonTreeNodes;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views.JsonTreeNodes;

/// <summary>
/// Tests for the <see cref="JsonValueTreeNode"/> class.
/// </summary>
public sealed class JsonValueTreeNodeTests
{
    [Fact]
    public void Constructor_WithValidText_SetsTextProperty()
    {
        // Arrange
        string expectedText = "Test Value";

        // Act
        JsonValueTreeNode node = new(expectedText);

        // Assert
        node.Text.Should().Be(expectedText);
    }

    [Fact]
    public void Constructor_WhenCalled_InitializesChildrenAsEmptyList()
    {
        // Arrange
        JsonValueTreeNode node = new("Test");

        // Act
        IList<ITreeNode> children = node.Children;

        // Assert
        children.Should().NotBeNull();
        children.Should().BeEmpty();
    }

    [Fact]
    public void ValueKind_WhenInitialized_StoresCorrectValue()
    {
        // Arrange
        JsonValueKind expectedKind = JsonValueKind.String;

        // Act
        JsonValueTreeNode node = new("Test") { ValueKind = expectedKind };

        // Assert
        node.ValueKind.Should().Be(expectedKind);
    }
}

using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.Dialogs;
using Refedle.Engine.Models.Actions;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views.Dialogs;

public sealed class OptionSelectorExtensionsTests
{
    [Fact]
    public void EnableAutoSelectAndVimKeys_DoesNotThrow()
    {
        // Arrange
        using OptionSelector<FilterOperator> selector = new();

        // Act
        Exception exception = Record.Exception(() => selector.EnableAutoSelectAndVimKeys());

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void EnableAutoSelectAndVimKeys_WithFreshSelector_DoesNotMutateValue()
    {
        // Arrange
        using OptionSelector<FilterOperator> selector = new();

        // Act
        selector.EnableAutoSelectAndVimKeys();

        // Assert
        FilterOperator? initialOperator = selector.Value;
        initialOperator.Should().Be(FilterOperator.Equals);
    }
}

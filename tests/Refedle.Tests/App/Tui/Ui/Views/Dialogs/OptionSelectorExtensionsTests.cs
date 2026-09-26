using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views.Dialogs;
using Refedle.Engine.Models.Actions;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.Ui.Views.Dialogs;

public sealed class OptionSelectorExtensionsTests
{
    [Fact]
    public void EnableAutoSelectAndVimKeys_DoesNotThrow()
    {
        // Arrange
        using var selector = new OptionSelector<FilterOperator>();

        // Act
        var exception = Record.Exception(() => selector.EnableAutoSelectAndVimKeys());

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void EnableAutoSelectAndVimKeys_WithFreshSelector_DoesNotMutateValue()
    {
        // Arrange
        using var selector = new OptionSelector<FilterOperator>();

        // Act
        selector.EnableAutoSelectAndVimKeys();

        // Assert
        var initialOperator = selector.Value;
        initialOperator.Should().Be(FilterOperator.Equals);
    }
}

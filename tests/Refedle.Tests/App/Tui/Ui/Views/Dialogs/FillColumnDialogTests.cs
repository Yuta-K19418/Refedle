using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views.Dialogs;

namespace Refedle.Tests.App.Tui.Ui.Views.Dialogs;

public sealed class FillColumnDialogTests
{
    [Fact]
    public void Constructor_SetsTitle_ToFillColumn()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FillColumnDialog(columnName);

        // Assert
        dialog.Title.Should().Be("Fill Column");
    }

    [Fact]
    public void Value_BeforeInteraction_IsEmptyString()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FillColumnDialog(columnName);

        // Assert
        dialog.Value.Should().Be(string.Empty);
    }

    [Fact]
    public void Confirmed_BeforeInteraction_IsFalse()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FillColumnDialog(columnName);

        // Assert
        dialog.Confirmed.Should().BeFalse();
    }
}

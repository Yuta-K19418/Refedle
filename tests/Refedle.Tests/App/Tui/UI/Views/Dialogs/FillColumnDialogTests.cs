using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.Dialogs;

namespace Refedle.Tests.App.Tui.UI.Views.Dialogs;

public sealed class FillColumnDialogTests
{
    [Fact]
    public void Constructor_SetsTitle_ToFillColumn()
    {
        // Arrange
        string columnName = "testColumn";

        // Act
        using FillColumnDialog dialog = new(columnName);

        // Assert
        dialog.Title.Should().Be("Fill Column");
    }

    [Fact]
    public void Value_BeforeInteraction_IsEmptyString()
    {
        // Arrange
        string columnName = "testColumn";

        // Act
        using FillColumnDialog dialog = new(columnName);

        // Assert
        dialog.Value.Should().Be(string.Empty);
    }

    [Fact]
    public void Confirmed_BeforeInteraction_IsFalse()
    {
        // Arrange
        string columnName = "testColumn";

        // Act
        using FillColumnDialog dialog = new(columnName);

        // Assert
        dialog.Confirmed.Should().BeFalse();
    }
}

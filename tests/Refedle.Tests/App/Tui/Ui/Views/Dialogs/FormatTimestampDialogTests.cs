using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views.Dialogs;

namespace Refedle.Tests.App.Tui.Ui.Views.Dialogs;

public sealed class FormatTimestampDialogTests
{
    [Fact]
    public void Constructor_SetsTitle_ToFormatTimestamp()
    {
        // Arrange
        var columnName = "created_at";

        // Act
        using var dialog = new FormatTimestampDialog(columnName);

        // Assert
        dialog.Title.Should().Be("Format Timestamp");
    }

    [Fact]
    public void TargetFormat_BeforeInteraction_IsEmptyString()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FormatTimestampDialog(columnName);

        // Assert
        dialog.TargetFormat.Should().Be(string.Empty);
    }

    [Fact]
    public void Confirmed_BeforeInteraction_IsFalse()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FormatTimestampDialog(columnName);

        // Assert
        dialog.Confirmed.Should().BeFalse();
    }
}

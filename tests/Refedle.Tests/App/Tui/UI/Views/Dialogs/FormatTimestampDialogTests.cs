using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.Dialogs;

namespace Refedle.Tests.App.Tui.UI.Views.Dialogs;

public sealed class FormatTimestampDialogTests
{
    [Fact]
    public void Constructor_SetsTitle_ToFormatTimestamp()
    {
        // Arrange
        string columnName = "created_at";

        // Act
        using FormatTimestampDialog dialog = new(columnName);

        // Assert
        dialog.Title.Should().Be("Format Timestamp");
    }

    [Fact]
    public void TargetFormat_BeforeInteraction_IsEmptyString()
    {
        // Arrange
        string columnName = "testColumn";

        // Act
        using FormatTimestampDialog dialog = new(columnName);

        // Assert
        dialog.TargetFormat.Should().Be(string.Empty);
    }

    [Fact]
    public void Confirmed_BeforeInteraction_IsFalse()
    {
        // Arrange
        string columnName = "testColumn";

        // Act
        using FormatTimestampDialog dialog = new(columnName);

        // Assert
        dialog.Confirmed.Should().BeFalse();
    }
}

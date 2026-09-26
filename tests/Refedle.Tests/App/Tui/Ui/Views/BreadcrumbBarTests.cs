using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.Tests.App.Tui.Ui.Views;

public sealed class BreadcrumbBarTests
{
    [Fact]
    public void SetPath_WithPath_UpdatesDisplayedText()
    {
        // Arrange
        using var bar = new BreadcrumbBar();
        IReadOnlyList<KeyPathSegment> path =
        [
            new KeyPathSegment("data", KeyPathSegmentKind.Key),
            new KeyPathSegment("[0]", KeyPathSegmentKind.Index),
        ];

        // Act
        bar.SetPath(path, collapseIndices: false);

        // Assert
        bar.Text.Should().Be("data[0]");
    }
}

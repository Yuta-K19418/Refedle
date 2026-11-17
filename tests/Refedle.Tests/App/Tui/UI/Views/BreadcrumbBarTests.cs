using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed class BreadcrumbBarTests
{
    [Fact]
    public void SetPath_WithPath_UpdatesDisplayedText()
    {
        // Arrange
        using BreadcrumbBar bar = new();
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

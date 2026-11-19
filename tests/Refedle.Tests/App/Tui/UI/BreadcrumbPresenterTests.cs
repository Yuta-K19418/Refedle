using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.Tests.App.Tui.UI;

public sealed class BreadcrumbPresenterTests
{
    private static readonly KeyPathSegment[] ListIndexPath =
    [
        new("list", KeyPathSegmentKind.Key),
        new("[0]", KeyPathSegmentKind.Index),
    ];

    [Theory]
    [InlineData(false, "list[0]")]
    [InlineData(true, "list[*]")]
    public void Update_WithIndexSegment_RendersLiteralOrCollapsedIndexAndStoresPath(bool collapseIndices, string expectedText)
    {
        // Arrange
        using AppState state = new();
        BreadcrumbPresenter presenter = new(state);

        // Act
        presenter.Update(ListIndexPath, collapseIndices);

        // Assert
        presenter.View.Text.Should().Be(expectedText);
        state.CurrentKeyPath.Should().Equal(ListIndexPath);
    }

    [Fact]
    public void Update_WithEmptyPath_RendersRoot()
    {
        // Arrange
        using AppState state = new();
        BreadcrumbPresenter presenter = new(state);

        // Act
        presenter.Update([], collapseIndices: false);

        // Assert
        presenter.View.Text.Should().Be("root");
        state.CurrentKeyPath.Should().BeEmpty();
    }

    [Fact]
    public void Clear_AfterUpdate_BlanksTextAndResetsPath()
    {
        // Arrange
        using AppState state = new();
        BreadcrumbPresenter presenter = new(state);
        presenter.Update(ListIndexPath, collapseIndices: false);

        // Act
        presenter.Clear();

        // Assert
        presenter.View.Text.Should().BeEmpty();
        state.CurrentKeyPath.Should().BeEmpty();
    }

    [Fact]
    public void Update_AfterClear_RendersPathAgain()
    {
        // Arrange
        using AppState state = new();
        BreadcrumbPresenter presenter = new(state);
        presenter.Clear();

        // Act
        presenter.Update(ListIndexPath, collapseIndices: false);

        // Assert
        presenter.View.Text.Should().Be("list[0]");
    }
}

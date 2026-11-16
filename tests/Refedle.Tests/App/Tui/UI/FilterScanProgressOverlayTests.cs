using AwesomeAssertions;
using Refedle.App.Tui.UI;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Refedle.Tests.App.Tui.UI;

public sealed class FilterScanProgressOverlayTests
{
    private static Window CreateParent() => new() { Width = Dim.Fill(), Height = Dim.Fill() };

    private static Label GetLabel(View parent) => parent.SubViews.OfType<Label>().Single();

    [Fact]
    public void Show_WithParent_AddsSpinnerAndLabelWithoutProgressBar()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var parent = CreateParent();
        using var overlay = new FilterScanProgressOverlay();

        // Act
        overlay.Show(parent);

        // Assert
        parent.SubViews.OfType<SpinnerView>().Should().ContainSingle();
        parent.SubViews.OfType<ProgressBar>().Should().BeEmpty();
        GetLabel(parent).Text.Should().Be(" Filtering… 0%");
    }

    [Fact]
    public void Show_WhenAlreadyShown_KeepsOnlyOneInstanceOfEachView()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var parent = CreateParent();
        using var overlay = new FilterScanProgressOverlay();
        overlay.Show(parent);

        // Act
        overlay.Show(parent);

        // Assert
        parent.SubViews.OfType<SpinnerView>().Should().ContainSingle();
        parent.SubViews.OfType<ProgressBar>().Should().BeEmpty();
        parent.SubViews.OfType<Label>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(0, 1000, " Filtering… 0%")]
    [InlineData(250, 1000, " Filtering… 25%")]
    [InlineData(1000, 1000, " Filtering… 100%")]
    [InlineData(1500, 1000, " Filtering… 100%")]
    public void Update_WithScanPosition_AdvancesPercentage(
        int scannedRows,
        int totalRows,
        string expectedText)
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var parent = CreateParent();
        using var overlay = new FilterScanProgressOverlay();
        overlay.Show(parent);

        // Act
        overlay.Update(scannedRows, totalRows);

        // Assert
        parent.SubViews.OfType<ProgressBar>().Should().BeEmpty();
        GetLabel(parent).Text.Should().Be(expectedText);
    }

    [Fact]
    public void Update_WithZeroTotalRows_ShowsZeroProgress()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var parent = CreateParent();
        using var overlay = new FilterScanProgressOverlay();
        overlay.Show(parent);

        // Act
        overlay.Update(0, 0);

        // Assert
        GetLabel(parent).Text.Should().Be(" Filtering… 0%");
    }

    [Fact]
    public void Update_WhenNotShown_DoesNotThrow()
    {
        // Arrange
        using var overlay = new FilterScanProgressOverlay();

        // Act
        var act = () => overlay.Update(1, 2);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dismiss_WithOverlayShown_RemovesAndDisposesAllViews()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var parent = CreateParent();
        using var overlay = new FilterScanProgressOverlay();
        overlay.Show(parent);
        var spinner = parent.SubViews.OfType<SpinnerView>().Single();
        var label = GetLabel(parent);
        var spinnerDisposed = false;
        var labelDisposed = false;
        spinner.Disposing += (_, _) => spinnerDisposed = true;
        label.Disposing += (_, _) => labelDisposed = true;

        // Act
        overlay.Dismiss();

        // Assert
        parent.SubViews.Should().NotContain(spinner);
        parent.SubViews.Should().NotContain(label);
        spinnerDisposed.Should().BeTrue();
        labelDisposed.Should().BeTrue();
    }

    [Fact]
    public void Show_WithParent_PlacesOverlayOnParentBottomRowOneColumnInsideRightEdge()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var parent = CreateParent();
        using var overlay = new FilterScanProgressOverlay();
        app.StopAfterFirstIteration = true;
        app.Begin(parent);

        // Act
        overlay.Show(parent);
        app.LayoutAndDraw();

        // Assert — the whole strip sits on the parent's bottom row when the host does not
        // scroll (its content size equals its viewport)
        var spinner = parent.SubViews.OfType<SpinnerView>().Single();
        var label = GetLabel(parent);
        var contentSize = parent.GetContentSize();
        spinner.Frame.Y.Should().Be(contentSize.Height - 1);
        label.Frame.Y.Should().Be(contentSize.Height - 1);
        label.Frame.Right.Should().Be(contentSize.Width - 1);
        spinner.Frame.X.Should().Be(label.Frame.X - 1);
    }

    [Fact]
    public void Show_WithParent_UsesWhiteOnDarkGraySchemeForAllViews()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var parent = CreateParent();
        using var overlay = new FilterScanProgressOverlay();

        // Act
        overlay.Show(parent);

        // Assert
        var expected = new Attribute(ColorName16.White, ColorName16.DarkGray);
        parent.SubViews.OfType<SpinnerView>().Single().GetScheme().Normal.Should().Be(expected);
        GetLabel(parent).GetScheme().Normal.Should().Be(expected);
    }

    [Fact]
    public void BringToFront_WithViewAddedAfterShow_MovesOverlayViewsAboveIt()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var parent = CreateParent();
        using var overlay = new FilterScanProgressOverlay();
        overlay.Show(parent);
        using var later = new View();
        parent.Add(later);

        // Act
        overlay.BringToFront();

        // Assert
        var order = parent.SubViews.ToList();
        var laterIndex = order.IndexOf(later);
        order.IndexOf(GetLabel(parent)).Should().BeGreaterThan(laterIndex);
        order.IndexOf(parent.SubViews.OfType<SpinnerView>().Single()).Should().BeGreaterThan(laterIndex);
    }

    [Fact]
    public void BringToFront_WhenNotShown_DoesNotThrow()
    {
        // Arrange
        using var overlay = new FilterScanProgressOverlay();

        // Act
        var act = overlay.BringToFront;

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dismiss_WhenNotShown_DoesNotThrow()
    {
        // Arrange
        using var overlay = new FilterScanProgressOverlay();

        // Act
        var act = overlay.Dismiss;

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void BringToFront_AfterDismiss_LeavesParentSubViewsUnchanged()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var parent = CreateParent();
        using var overlay = new FilterScanProgressOverlay();
        using var sibling = new View();
        overlay.Show(parent);
        parent.Add(sibling);
        overlay.Dismiss();
        var before = parent.SubViews.ToList();

        // Act
        overlay.BringToFront();

        // Assert
        parent.SubViews.Should().Equal(before);
    }
}

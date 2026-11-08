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

    private static ProgressBar GetProgressBar(View parent) => parent.SubViews.OfType<ProgressBar>().Single();

    private static Label GetLabel(View parent) => parent.SubViews.OfType<Label>().Single();

    [Fact]
    public void Show_WithParent_AddsSpinnerProgressBarAndLabel()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var parent = CreateParent();
        using var overlay = new FilterScanProgressOverlay();

        // Act
        overlay.Show(parent);

        // Assert
        parent.SubViews.OfType<SpinnerView>().Should().ContainSingle();
        parent.SubViews.OfType<ProgressBar>().Should().ContainSingle();
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
        parent.SubViews.OfType<ProgressBar>().Should().ContainSingle();
        parent.SubViews.OfType<Label>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(0, 1000, 0f, " Filtering… 0%")]
    [InlineData(250, 1000, 0.25f, " Filtering… 25%")]
    [InlineData(1000, 1000, 1f, " Filtering… 100%")]
    [InlineData(1500, 1000, 1f, " Filtering… 100%")]
    public void Update_WithScanPosition_AdvancesBarAndPercentage(
        int scannedRows,
        int totalRows,
        float expectedFraction,
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
        GetProgressBar(parent).Fraction.Should().BeApproximately(expectedFraction, 0.001f);
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
        GetProgressBar(parent).Fraction.Should().Be(0f);
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
        var bar = GetProgressBar(parent);
        var label = GetLabel(parent);
        var spinnerDisposed = false;
        var barDisposed = false;
        var labelDisposed = false;
        spinner.Disposing += (_, _) => spinnerDisposed = true;
        bar.Disposing += (_, _) => barDisposed = true;
        label.Disposing += (_, _) => labelDisposed = true;

        // Act
        overlay.Dismiss();

        // Assert
        parent.SubViews.Should().NotContain(spinner);
        parent.SubViews.Should().NotContain(bar);
        parent.SubViews.Should().NotContain(label);
        spinnerDisposed.Should().BeTrue();
        barDisposed.Should().BeTrue();
        labelDisposed.Should().BeTrue();
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
        GetProgressBar(parent).GetScheme().Normal.Should().Be(expected);
        GetLabel(parent).GetScheme().Normal.Should().Be(expected);
    }
}

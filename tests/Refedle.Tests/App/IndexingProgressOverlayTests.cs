using AwesomeAssertions;
using Refedle.App;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Refedle.Tests.App;

public sealed class IndexingProgressOverlayTests
{
    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        app.Driver.Should().NotBeNull();
        app.Driver.SetScreenSize(80, 25);
        return app;
    }

    private static Window CreateParent() => new() { Width = Dim.Fill(), Height = Dim.Fill() };

    private static Label GetLabel(View parent) => parent.SubViews.OfType<Label>().Single();

    [Fact]
    public void Show_WithParent_AddsSpinnerAndLabelWithInitialText()
    {
        // Arrange
        using var app = CreateTestApp();
        using var parent = CreateParent();
        using var overlay = new IndexingProgressOverlay();

        // Act
        overlay.Show(parent);

        // Assert
        parent.SubViews.OfType<SpinnerView>().Should().ContainSingle();
        GetLabel(parent).Text.Should().Be(" Indexing…");
    }

    [Fact]
    public void Show_WhenAlreadyShown_KeepsOnlyOneSpinnerAndLabel()
    {
        // Arrange
        using var app = CreateTestApp();
        using var parent = CreateParent();
        using var overlay = new IndexingProgressOverlay();
        overlay.Show(parent);

        // Act
        overlay.Show(parent);

        // Assert
        parent.SubViews.OfType<SpinnerView>().Should().ContainSingle();
        parent.SubViews.OfType<Label>().Should().ContainSingle();
    }

    [Fact]
    public void Show_WithParent_UsesAutoSpinningDotsSpinnerWithOverlayScheme()
    {
        // Arrange
        using var app = CreateTestApp();
        using var parent = CreateParent();
        using var overlay = new IndexingProgressOverlay();

        // Act
        overlay.Show(parent);

        // Assert
        var spinner = parent.SubViews.OfType<SpinnerView>().Single();
        spinner.AutoSpin.Should().BeTrue();
        spinner.Style.Sequence.Should().Equal("⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏");
        spinner.SchemeName.Should().Be("IndexingOverlay");
        GetLabel(parent).SchemeName.Should().Be("IndexingOverlay");
    }

    [Fact]
    public void Show_WithParent_UsesWhiteOnDarkGrayWithoutBorderForOverlayViews()
    {
        // Arrange
        using var app = CreateTestApp();
        using var parent = CreateParent();
        using var overlay = new IndexingProgressOverlay();

        // Act
        overlay.Show(parent);

        // Assert
        var spinner = parent.SubViews.OfType<SpinnerView>().Single();
        var label = GetLabel(parent);
        var expected = new Attribute(ColorName16.White, ColorName16.DarkGray);
        spinner.GetScheme().Normal.Should().Be(expected);
        label.GetScheme().Normal.Should().Be(expected);
        spinner.Border.Thickness.Should().Be(Thickness.Empty);
        label.Border.Thickness.Should().Be(Thickness.Empty);
    }

    [Fact]
    public void Show_WithParent_PlacesOverlayOneRowAboveParentBottomOneColumnInsideRightEdge()
    {
        // Arrange
        using var app = CreateTestApp();
        using var parent = CreateParent();
        using var overlay = new IndexingProgressOverlay();
        app.StopAfterFirstIteration = true;
        app.Begin(parent);

        // Act
        overlay.Show(parent);
        app.LayoutAndDraw();

        // Assert
        var spinner = parent.SubViews.OfType<SpinnerView>().Single();
        var label = GetLabel(parent);
        var contentSize = parent.GetContentSize();
        spinner.Frame.Y.Should().Be(contentSize.Height - 2);
        label.Frame.Y.Should().Be(contentSize.Height - 2);
        label.Frame.Right.Should().Be(contentSize.Width - 1);
        spinner.Frame.X.Should().Be(label.Frame.X - 1);
    }

    [Theory]
    [InlineData(1_288_490_189L, 2_147_483_648L, " Indexing… 60%  1.2GB / 2.0GB")]
    [InlineData(536_870_912L, 1_073_741_824L, " Indexing… 50%  512.0MB / 1.0GB")]
    public void Update_WithProgressValues_ShowsPercentAndBytes(long bytesRead, long fileSize, string expectedText)
    {
        // Arrange
        using var app = CreateTestApp();
        using var parent = CreateParent();
        using var overlay = new IndexingProgressOverlay();
        overlay.Show(parent);

        // Act
        overlay.Update(bytesRead, fileSize);

        // Assert
        GetLabel(parent).Text.Should().Be(expectedText);
    }

    [Theory]
    [InlineData(1023L, " Indexing… 100%  1023B / 1023B")]
    [InlineData(1024L, " Indexing… 100%  1.0KB / 1.0KB")]
    [InlineData(1_047_552L, " Indexing… 100%  1023.0KB / 1023.0KB")]
    [InlineData(1_048_576L, " Indexing… 100%  1.0MB / 1.0MB")]
    [InlineData(1_072_693_248L, " Indexing… 100%  1023.0MB / 1023.0MB")]
    [InlineData(1_073_741_824L, " Indexing… 100%  1.0GB / 1.0GB")]
    public void Update_WithSizeAtUnitBoundary_ShowsBytesInExpectedUnit(long size, string expectedText)
    {
        // Arrange
        using var app = CreateTestApp();
        using var parent = CreateParent();
        using var overlay = new IndexingProgressOverlay();
        overlay.Show(parent);

        // Act
        overlay.Update(size, size);

        // Assert
        GetLabel(parent).Text.Should().Be(expectedText);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Update_WithNonPositiveFileSize_KeepsInitialText(long fileSize)
    {
        // Arrange
        using var app = CreateTestApp();
        using var parent = CreateParent();
        using var overlay = new IndexingProgressOverlay();
        overlay.Show(parent);

        // Act
        overlay.Update(1024L, fileSize);

        // Assert
        GetLabel(parent).Text.Should().Be(" Indexing…");
    }

    [Fact]
    public void Update_WhenNotShown_DoesNotThrow()
    {
        // Arrange
        using var overlay = new IndexingProgressOverlay();

        // Act
        var act = () => overlay.Update(1L, 2L);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dismiss_WithOverlayShown_RemovesAndDisposesSpinnerAndLabel()
    {
        // Arrange
        using var app = CreateTestApp();
        using var parent = CreateParent();
        using var overlay = new IndexingProgressOverlay();
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
    public void Dismiss_WhenNotShown_DoesNotThrow()
    {
        // Arrange
        using var overlay = new IndexingProgressOverlay();

        // Act
        var act = overlay.Dismiss;

        // Assert
        act.Should().NotThrow();
    }
}

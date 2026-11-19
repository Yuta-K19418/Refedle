using AwesomeAssertions;
using Refedle.App.Tui.UI;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.ViewBase;

namespace Refedle.Tests.App.Tui.UI;

public sealed partial class LazyFilterScanTrackerTests
{
    [Fact]
    public void Constructor_WithNullFilterScanRunner_ThrowsArgumentNullException()
    {
        // Arrange
        using View host = new();

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            null!,
            static _ => { },
            static () => CancellationToken.None,
            static () => { },
            static _ => { },
            host,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUiThreadInvoke_ThrowsArgumentNullException()
    {
        // Arrange
        using View host = new();

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            new PendingFilterScanRunner(),
            null!,
            static () => CancellationToken.None,
            static () => { },
            static _ => { },
            host,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullParentToken_ThrowsArgumentNullException()
    {
        // Arrange
        using View host = new();

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            new PendingFilterScanRunner(),
            static _ => { },
            null!,
            static () => { },
            static _ => { },
            host,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRefreshStatusBarHints_ThrowsArgumentNullException()
    {
        // Arrange
        using View host = new();

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            new PendingFilterScanRunner(),
            static _ => { },
            static () => CancellationToken.None,
            null!,
            static _ => { },
            host,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullShowError_ThrowsArgumentNullException()
    {
        // Arrange
        using View host = new();

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            new PendingFilterScanRunner(),
            static _ => { },
            static () => CancellationToken.None,
            static () => { },
            null!,
            host,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullGetCurrentView_ThrowsArgumentNullException()
    {
        // Arrange
        using View host = new();

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            new PendingFilterScanRunner(),
            static _ => { },
            static () => CancellationToken.None,
            static () => { },
            static _ => { },
            host,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullOverlayHost_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup required)

        // Act
        Func<LazyFilterScanTracker> act = () => new LazyFilterScanTracker(
            new PendingFilterScanRunner(),
            static _ => { },
            static () => CancellationToken.None,
            static () => { },
            static _ => { },
            null!,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}

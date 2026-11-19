using AwesomeAssertions;
using Refedle.App.Tui.UI;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;

namespace Refedle.Tests.App.Tui.UI;

public sealed class ContentViewHostTests
{
    [Fact]
    public void CurrentView_BeforeFirstReplace_IsNullAndContainerIsEmpty()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using View anchor = new();

        // Act
        using ContentViewHost host = new(anchor);

        // Assert
        host.CurrentView.Should().BeNull();
        host.Container.SubViews.Should().BeEmpty();
    }

    [Fact]
    public void Replace_WithFirstView_AddsItToContainerAndFillsIt()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using View anchor = new();
        using ContentViewHost host = new(anchor);
        using View view = new() { X = 5, Y = 5, Width = 3, Height = 3 };

        // Act
        host.Replace(view);

        // Assert
        host.CurrentView.Should().BeSameAs(view);
        host.Container.SubViews.Should().ContainSingle().Which.Should().BeSameAs(view);
        view.X.Should().Be(Pos.Absolute(0));
        view.Y.Should().Be(Pos.Absolute(0));
        view.Width.Should().Be(Dim.Fill());
        view.Height.Should().Be(Dim.Fill());
    }

    [Fact]
    public void Replace_WithSecondView_RemovesAndDisposesPreviousView()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using View anchor = new();
        using ContentViewHost host = new(anchor);
        using DisposalTrackingView first = new();
        using View second = new();
        host.Replace(first);

        // Act
        host.Replace(second);

        // Assert
        first.WasDisposed.Should().BeTrue();
        host.CurrentView.Should().BeSameAs(second);
        host.Container.SubViews.Should().ContainSingle().Which.Should().BeSameAs(second);
    }

    [Fact]
    public void Dispose_WithCurrentView_DisposesIt()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using View anchor = new();
        using ContentViewHost host = new(anchor);
        using DisposalTrackingView view = new();
        host.Replace(view);

        // Act
        host.Dispose();

        // Assert
        view.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_WithoutCurrentView_DoesNotThrow()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using View anchor = new();
        using ContentViewHost host = new(anchor);

        // Act
        Action act = host.Dispose;

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Container_IsPlacedBelowAnchor()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using View anchor = new() { Height = 1 };

        // Act
        using ContentViewHost host = new(anchor);

        // Assert
        host.Container.Y.Should().Be(Pos.Bottom(anchor));
        host.Container.CanFocus.Should().BeTrue();
    }

    private sealed class DisposalTrackingView : View
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}

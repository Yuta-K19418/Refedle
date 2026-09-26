using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views;

namespace Refedle.Tests.App.Tui.Ui.Views;

public sealed class ViewportStateTests
{
    [Fact]
    public void ScrollDown_ShouldIncreaseTopRow()
    {
        var state = new ViewportState
        {
            TopRow = 0,
            VisibleRowCount = 20,
            TotalRowCount = 100
        };

        state.ScrollDown(5);

        state.TopRow.Should().Be(5);
    }

    [Fact]
    public void ScrollDown_ShouldNotExceedMaxTopRow()
    {
        var state = new ViewportState
        {
            TopRow = 90,
            VisibleRowCount = 20,
            TotalRowCount = 100
        };

        state.ScrollDown(50);

        state.TopRow.Should().Be(80); // Max is TotalRowCount - VisibleRowCount
    }

    [Fact]
    public void ScrollUp_ShouldDecreaseTopRow()
    {
        var state = new ViewportState
        {
            TopRow = 10,
            VisibleRowCount = 20,
            TotalRowCount = 100
        };

        state.ScrollUp(5);

        state.TopRow.Should().Be(5);
    }

    [Fact]
    public void ScrollUp_ShouldNotGoBelowZero()
    {
        var state = new ViewportState
        {
            TopRow = 3,
            VisibleRowCount = 20,
            TotalRowCount = 100
        };

        state.ScrollUp(10);

        state.TopRow.Should().Be(0);
    }

    [Fact]
    public void PageDown_ShouldScrollByVisibleRowCount()
    {
        var state = new ViewportState
        {
            TopRow = 0,
            VisibleRowCount = 20,
            TotalRowCount = 100
        };

        state.PageDown();

        state.TopRow.Should().Be(20);
    }

    [Fact]
    public void PageUp_ShouldScrollByVisibleRowCount()
    {
        var state = new ViewportState
        {
            TopRow = 40,
            VisibleRowCount = 20,
            TotalRowCount = 100
        };

        state.PageUp();

        state.TopRow.Should().Be(20);
    }

    [Fact]
    public void JumpToTop_ShouldSetTopRowToZero()
    {
        var state = new ViewportState
        {
            TopRow = 50,
            VisibleRowCount = 20,
            TotalRowCount = 100
        };

        state.JumpToTop();

        state.TopRow.Should().Be(0);
    }

    [Fact]
    public void JumpToBottom_ShouldSetTopRowToMaxValue()
    {
        var state = new ViewportState
        {
            TopRow = 0,
            VisibleRowCount = 20,
            TotalRowCount = 100
        };

        state.JumpToBottom();

        state.TopRow.Should().Be(80); // TotalRowCount - VisibleRowCount
    }

    [Fact]
    public void BottomRow_ShouldReturnCorrectValue()
    {
        var state = new ViewportState
        {
            TopRow = 10,
            VisibleRowCount = 20,
            TotalRowCount = 100
        };

        state.BottomRow.Should().Be(29); // TopRow + VisibleRowCount - 1
    }

    [Fact]
    public void BottomRow_ShouldNotExceedTotalRowCount()
    {
        var state = new ViewportState
        {
            TopRow = 95,
            VisibleRowCount = 20,
            TotalRowCount = 100
        };

        state.BottomRow.Should().Be(99); // TotalRowCount - 1
    }

    [Fact]
    public void JumpToBottom_WithSmallTotalRowCount_ShouldSetTopRowToZero()
    {
        var state = new ViewportState
        {
            TopRow = 5,
            VisibleRowCount = 20,
            TotalRowCount = 10
        };

        state.JumpToBottom();

        state.TopRow.Should().Be(0); // Max(0, TotalRowCount - VisibleRowCount)
    }
}

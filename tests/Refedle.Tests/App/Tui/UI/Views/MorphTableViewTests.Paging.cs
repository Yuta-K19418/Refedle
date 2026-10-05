using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

// Paging behavior of MorphTableView over a PagedTableSource with a small page size.
public sealed partial class MorphTableViewTests
{
    private sealed class NumberedTableSource(long totalRows) : IExtendedTableSource
    {
        public long TotalRows { get; set; } = totalRows;
        public int Columns => 3;
        public string[] ColumnNames => ["a", "b", "c"];
        public string[] RawColumnNames => ["a", "b", "c"];
        public object this[long row, int col] => $"row{row}";
    }

    private sealed class PagedViewFixture : IDisposable
    {
        public PagedViewFixture(long totalRows, int pageSize)
        {
            Source = new NumberedTableSource(totalRows);
            Paged = new PagedTableSource(Source, pageSize);
            View = new ConcreteMorphTableView
            {
                Table = Paged,
            };
            View.SetSelection(0, 0, false);
        }

        public NumberedTableSource Source { get; }
        public PagedTableSource Paged { get; }
        public ConcreteMorphTableView View { get; }

        public void Dispose() => View.Dispose();
    }

    private static PagedViewFixture CreatePagedView(long totalRows, int pageSize = 3) => new(totalRows, pageSize);

    private static long SelectedAbsoluteRow(PagedViewFixture fixture)
    {
        var selectedCell = fixture.View.Value.Should().BeOfType<TableSelection>().Which.SelectedCell;
        return fixture.Paged.WindowStart + selectedCell.Y;
    }

    private static void PressKey(ConcreteMorphTableView view, KeyCode keyCode, int times)
    {
        for (var press = 0; press < times; press++)
        {
            _ = view.ProcessKey(new Key(keyCode));
        }
    }

    [Fact]
    public void CollectionNavigator_AfterConstruction_IsNullToDisableTypeToSearch()
    {
        // Arrange
        using var app = CreateTestApp();

        // Act
        using var view = new ConcreteMorphTableView();

        // Assert
        view.CollectionNavigator.Should().BeNull();
    }

    [Fact]
    public void SetSelection_IntoAdjacentPage_UpdatesPageNumberWithoutShiftingWindow()
    {
        // Arrange — page size 3: page 1 window spans rows 0-5, page 2 starts at row 3
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 20);
        var view = fixture.View;
        var paged = fixture.Paged;

        // Act
        view.SetSelection(0, 3, false);

        // Assert
        paged.CurrentPageNumber.Should().Be(2);
        paged.WindowStart.Should().Be(0);
        view.Value.Should().BeOfType<TableSelection>().Which.SelectedCell.Y.Should().Be(3);
    }

    [Fact]
    public void SetSelection_BeyondAdjacentPage_ShiftsWindowAndKeepsAbsoluteRow()
    {
        // Arrange — already on page 2 (window rows 0-5); row 6 opens page 3
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 20);
        var view = fixture.View;
        var paged = fixture.Paged;
        view.SetSelection(0, 3, false);

        // Act
        view.SetSelection(0, 6, false);

        // Assert
        paged.CurrentPageNumber.Should().Be(3);
        paged.WindowStart.Should().Be(3);
        view.Value.Should().BeOfType<TableSelection>().Which.SelectedCell.Y.Should().Be(3);
    }

    [Fact]
    public void SetSelection_BackIntoPreviousPage_ShiftsWindowBackwardAndKeepsAbsoluteRow()
    {
        // Arrange — walk onto page 3 in two steps: the initial window only spans pages 1-2
        // (6 rows), so a direct SetSelection to row 6 would be rejected by the bounds check
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 20);
        var view = fixture.View;
        var paged = fixture.Paged;
        view.SetSelection(0, 3, false);
        view.SetSelection(0, 6, false);

        // Act
        view.SetSelection(0, 2, false);

        // Assert
        paged.CurrentPageNumber.Should().Be(2);
        paged.WindowStart.Should().Be(0);
        view.Value.Should().BeOfType<TableSelection>().Which.SelectedCell.Y.Should().Be(5);
    }

    [Fact]
    public void ProcessKey_JAcrossPageBoundary_KeepsAbsoluteRowAdvancing()
    {
        // Arrange — seven j presses walk the cursor from row 0 to row 7 (page 3)
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 20);
        var view = fixture.View;
        var paged = fixture.Paged;

        // Act
        PressKey(view, KeyCode.J, times: 7);

        // Assert
        paged.WindowStart.Should().Be(3);
        var selectedCell = view.Value.Should().BeOfType<TableSelection>().Which.SelectedCell;
        (paged.WindowStart + selectedCell.Y).Should().Be(7);
    }

    [Fact]
    public void ProcessKey_KAcrossPageBoundary_KeepsAbsoluteRowDecreasing()
    {
        // Arrange — walk to row 7 (page 3, window starts at row 3), then step back past the window top
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 20);
        PressKey(fixture.View, KeyCode.J, times: 7);
        var windowStartBefore = fixture.Paged.WindowStart;

        // Act
        PressKey(fixture.View, KeyCode.K, times: 5);

        // Assert
        fixture.Paged.WindowStart.Should().BeLessThan(windowStartBefore);
        SelectedAbsoluteRow(fixture).Should().Be(2);
    }

    [Fact]
    public void ProcessKey_KOnFirstRow_StaysOnFirstRow()
    {
        // Arrange
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 20);

        // Act
        PressKey(fixture.View, KeyCode.K, times: 2);

        // Assert
        fixture.Paged.CurrentPageNumber.Should().Be(1);
        SelectedAbsoluteRow(fixture).Should().Be(0);
    }

    [Fact]
    public void ProcessKey_JOnLastRow_StaysOnLastRow()
    {
        // Arrange
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 20);
        PressKey(fixture.View, KeyCode.G | KeyCode.ShiftMask, times: 1);

        // Act
        PressKey(fixture.View, KeyCode.J, times: 2);

        // Assert
        fixture.Paged.CurrentPageNumber.Should().Be(7);
        SelectedAbsoluteRow(fixture).Should().Be(19);
    }

    [Fact]
    public void ProcessKey_GGFromLaterPage_MovesToFirstRowAndKeepsColumn()
    {
        // Arrange — start on page 3 in column 1; the initial window only spans pages 1-2
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 20);
        var view = fixture.View;
        view.SetSelection(1, 3, false);
        view.SetSelection(1, 6, false);

        // Act
        PressKey(view, KeyCode.G, times: 2);

        // Assert
        fixture.Paged.CurrentPageNumber.Should().Be(1);
        fixture.Paged.WindowStart.Should().Be(0);
        var selectedCell = view.Value.Should().BeOfType<TableSelection>().Which.SelectedCell;
        selectedCell.Y.Should().Be(0);
        selectedCell.X.Should().Be(1);
        view.RowOffset.Should().Be(0);
    }

    [Fact]
    public void ProcessKey_ShiftGFromFirstPage_MovesToLastRowAndKeepsColumn()
    {
        // Arrange — 20 rows in pages of 3: page 7 ends at row 19, window rows 15-19
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 20);
        var view = fixture.View;
        view.SetSelection(1, 0, false);

        // Act
        PressKey(view, KeyCode.G | KeyCode.ShiftMask, times: 1);

        // Assert
        fixture.Paged.CurrentPageNumber.Should().Be(7);
        fixture.Paged.WindowStart.Should().Be(15);
        var selectedCell = view.Value.Should().BeOfType<TableSelection>().Which.SelectedCell;
        selectedCell.Y.Should().Be(4);
        selectedCell.X.Should().Be(1);
        view.RowOffset.Should().BeLessThanOrEqualTo(4);
        SelectedAbsoluteRow(fixture).Should().Be(19);
    }

    [Fact]
    public void ProcessKey_GGOnEmptyTable_DoesNotThrowAndStaysOnFirstPage()
    {
        // Arrange
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 0);

        // Act
        var act = () => PressKey(fixture.View, KeyCode.G, times: 2);

        // Assert
        act.Should().NotThrow();
        fixture.Paged.CurrentPageNumber.Should().Be(1);
        fixture.Paged.WindowStart.Should().Be(0);
    }

    [Fact]
    public void ProcessKey_ShiftGOnEmptyTable_DoesNotThrowAndStaysOnFirstPage()
    {
        // Arrange
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 0);

        // Act
        var act = () => PressKey(fixture.View, KeyCode.G | KeyCode.ShiftMask, times: 1);

        // Assert
        act.Should().NotThrow();
        fixture.Paged.CurrentPageNumber.Should().Be(1);
        fixture.Paged.WindowStart.Should().Be(0);
    }

    [Fact]
    public void ProcessKey_ShiftGWhenLastRowDoesNotFitViewport_ScrollsToPositiveRowOffset()
    {
        // Arrange — far more rows per page than the test screen can show
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 300, pageSize: 100);

        // Act
        PressKey(fixture.View, KeyCode.G | KeyCode.ShiftMask, times: 1);

        // Assert
        SelectedAbsoluteRow(fixture).Should().Be(299);
        fixture.View.RowOffset.Should().BePositive();
    }

    [Theory]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(11, 2)]
    public void SetSelection_AroundPageBoundary_UpdatesPageAndKeepsAbsoluteRow(long absoluteRow, long expectedPage)
    {
        // Arrange — page size 10: row 10 is the first row of page 2
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 100, pageSize: 10);

        // Act
        fixture.View.SetSelection(0, (int)absoluteRow, false);

        // Assert
        fixture.Paged.CurrentPageNumber.Should().Be(expectedPage);
        SelectedAbsoluteRow(fixture).Should().Be(absoluteRow);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(25)]
    public void ProcessKey_ShiftGWithFullOrPartialLastPage_SelectsLastRow(long totalRows)
    {
        // Arrange — page size 10: 30 rows end on a full page, 25 rows end on a partial one
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows, pageSize: 10);

        // Act
        PressKey(fixture.View, KeyCode.G | KeyCode.ShiftMask, times: 1);

        // Assert
        fixture.Paged.CurrentPageNumber.Should().Be(3);
        SelectedAbsoluteRow(fixture).Should().Be(totalRows - 1);
    }

    [Fact]
    public void Update_AfterTotalRowsGrowsOnLaterPage_KeepsPageWindowOffsetAndSelectedRow()
    {
        // Arrange — park on page 3, then let the background index append rows
        using var app = CreateTestApp();
        using var fixture = CreatePagedView(totalRows: 20);
        var view = fixture.View;
        PressKey(view, KeyCode.J, times: 7);
        var pageBefore = fixture.Paged.CurrentPageNumber;
        var windowStartBefore = fixture.Paged.WindowStart;
        var rowOffsetBefore = view.RowOffset;
        var selectedRowBefore = SelectedAbsoluteRow(fixture);

        // Act
        fixture.Source.TotalRows = 40;
        view.Update();

        // Assert
        fixture.Paged.CurrentPageNumber.Should().Be(pageBefore);
        fixture.Paged.WindowStart.Should().Be(windowStartBefore);
        view.RowOffset.Should().Be(rowOffsetBefore);
        SelectedAbsoluteRow(fixture).Should().Be(selectedRowBefore);
    }
}

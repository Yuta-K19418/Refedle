using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed class PagedTableSourceTests
{
    private sealed class RecordingTableSource(long totalRows) : IExtendedTableSource
    {
        public long TotalRows { get; set; } = totalRows;
        public int Columns => 1;
        public string[] ColumnNames => ["value"];
        public string[] RawColumnNames => ["value"];
        public List<long> RequestedRows { get; } = [];

        public object this[long row, int col]
        {
            get
            {
                RequestedRows.Add(row);
                return row;
            }
        }
    }

    private sealed class DisposableTableSource : IExtendedTableSource, IDisposable
    {
        public bool IsDisposed => DisposeCount > 0;
        public int DisposeCount { get; private set; }
        public long TotalRows => 0;
        public int Columns => 0;
        public string[] ColumnNames => [];
        public string[] RawColumnNames => [];
        public object this[long row, int col] => throw new NotImplementedException();
        public void Dispose() => DisposeCount++;
    }

    [Fact]
    public void Constructor_WithNullInner_ThrowsArgumentNullException()
    {
        // Arrange / Act
        var act = () => new PagedTableSource(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositivePageSize_ThrowsArgumentOutOfRangeException(int pageSize)
    {
        // Arrange
        var inner = new RecordingTableSource(totalRows: 10);

        // Act
        var act = () => new PagedTableSource(inner, pageSize);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithPageSizeAboveOneThirdOfIntMaxValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange — three pages of this size would no longer fit in int.MaxValue
        var inner = new RecordingTableSource(totalRows: 10);

        // Act
        var act = () => new PagedTableSource(inner, int.MaxValue / 3 + 1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rows_OnFirstPage_ShowsTwoPages()
    {
        // Arrange — 10 rows per page, 100 rows total; page 1 has no previous page
        var inner = new RecordingTableSource(totalRows: 100);
        using var source = new PagedTableSource(inner, pageSize: 10);

        // Act
        var rows = source.Rows;

        // Assert
        rows.Should().Be(20);
        source.WindowStart.Should().Be(0);
    }

    [Fact]
    public void Rows_OnMiddlePage_ShowsThreePages()
    {
        // Arrange
        var inner = new RecordingTableSource(totalRows: 100);
        using var source = new PagedTableSource(inner, pageSize: 10);
        source.CurrentPageNumber = 3;

        // Act
        var rows = source.Rows;

        // Assert
        rows.Should().Be(30);
        source.WindowStart.Should().Be(10);
    }

    [Fact]
    public void Rows_OnLastPage_ShowsRemainingRowsOnly()
    {
        // Arrange — 95 rows = 9 full pages + 5 rows; page 10 shows pages 9-10
        var inner = new RecordingTableSource(totalRows: 95);
        using var source = new PagedTableSource(inner, pageSize: 10);
        source.CurrentPageNumber = 10;

        // Act
        var rows = source.Rows;

        // Assert
        rows.Should().Be(15);
        source.WindowStart.Should().Be(80);
    }

    [Fact]
    public void Rows_WhenTableSmallerThanOnePage_ShowsAllRows()
    {
        // Arrange
        var inner = new RecordingTableSource(totalRows: 7);
        using var source = new PagedTableSource(inner, pageSize: 10);

        // Act
        var rows = source.Rows;

        // Assert
        rows.Should().Be(7);
    }

    [Fact]
    public void Rows_WhenTotalRowsIsZero_ShowsZeroRows()
    {
        // Arrange
        var inner = new RecordingTableSource(totalRows: 0);
        using var source = new PagedTableSource(inner, pageSize: 10);

        // Act
        var rows = source.Rows;

        // Assert
        rows.Should().Be(0);
    }

    [Fact]
    public void Rows_AfterTotalRowsGrows_ExtendsWindowWithoutMovingIt()
    {
        // Arrange — background indexing keeps appending rows while the user stays put
        var inner = new RecordingTableSource(totalRows: 45);
        using var source = new PagedTableSource(inner, pageSize: 10);
        source.CurrentPageNumber = 5;

        // Act
        inner.TotalRows = 80;
        var rows = source.Rows;

        // Assert
        source.WindowStart.Should().Be(30);
        rows.Should().Be(30);
    }

    [Fact]
    public void Indexer_OnMiddlePage_ReadsInnerRowAtWindowOffset()
    {
        // Arrange — page 3's window covers pages 2-4, so it starts at absolute row 10
        var inner = new RecordingTableSource(totalRows: 100);
        using var source = new PagedTableSource(inner, pageSize: 10);
        source.CurrentPageNumber = 3;

        // Act
        _ = source[5, 0];

        // Assert
        inner.RequestedRows.Should().ContainSingle().Which.Should().Be(15L);
    }

    [Fact]
    public void ColumnNamesAndColumns_DelegateToInner()
    {
        // Arrange
        var inner = new RecordingTableSource(totalRows: 10);
        using var source = new PagedTableSource(inner, pageSize: 10);

        // Act
        var columnNames = source.ColumnNames;
        var columns = source.Columns;

        // Assert
        columnNames.Should().Equal(["value"]);
        columns.Should().Be(1);
    }

    [Fact]
    public void Inner_ExposesWrappedSource()
    {
        // Arrange
        var inner = new RecordingTableSource(totalRows: 10);
        using var source = new PagedTableSource(inner, pageSize: 10);

        // Act
        var recovered = source.Inner;

        // Assert
        recovered.Should().BeSameAs(inner);
    }

    [Fact]
    public void SwitchPageFor_RowInNextPage_ShiftsWindowForwardByPage()
    {
        // Arrange — page 3's window covers pages 2-4 (start row 10); row 40 opens page 5
        var inner = new RecordingTableSource(totalRows: 100);
        using var source = new PagedTableSource(inner, pageSize: 10);
        source.CurrentPageNumber = 3;

        // Act
        var windowShift = source.SwitchPageFor(40);

        // Assert — the window slides to pages 4-6, shifting its start by two pages
        windowShift.Should().Be(20);
        source.CurrentPageNumber.Should().Be(5);
        source.WindowStart.Should().Be(30);
    }

    [Fact]
    public void SwitchPageFor_RowOnCurrentPage_ReturnsZeroShift()
    {
        // Arrange
        var inner = new RecordingTableSource(totalRows: 100);
        using var source = new PagedTableSource(inner, pageSize: 10);
        source.CurrentPageNumber = 3;

        // Act
        var windowShift = source.SwitchPageFor(25);

        // Assert
        windowShift.Should().Be(0);
        source.CurrentPageNumber.Should().Be(3);
    }

    [Fact]
    public void SwitchPageFor_RowOnAdjacentPageInsideWindow_ReturnsZeroShiftButUpdatesPage()
    {
        // Arrange — page 1 window spans pages 1-2; row 12 opens page 2, window stays
        var inner = new RecordingTableSource(totalRows: 100);
        using var source = new PagedTableSource(inner, pageSize: 10);

        // Act
        var windowShift = source.SwitchPageFor(12);

        // Assert
        windowShift.Should().Be(0);
        source.CurrentPageNumber.Should().Be(2);
        source.WindowStart.Should().Be(0);
    }

    [Fact]
    public void SwitchPageFor_RowBeyondEnd_ClampsToLastPage()
    {
        // Arrange
        var inner = new RecordingTableSource(totalRows: 95);
        using var source = new PagedTableSource(inner, pageSize: 10);

        // Act
        var windowShift = source.SwitchPageFor(94);

        // Assert
        source.CurrentPageNumber.Should().Be(10);
        source.WindowStart.Should().Be(80);
        windowShift.Should().Be(80);
    }

    [Fact]
    public void CurrentPageNumber_WhenAssignedBeyondLastPage_ClampsToPageCount()
    {
        // Arrange
        var inner = new RecordingTableSource(totalRows: 95);
        using var source = new PagedTableSource(inner, pageSize: 10);

        // Act
        source.CurrentPageNumber = 99;

        // Assert
        source.CurrentPageNumber.Should().Be(10);
    }

    [Fact]
    public void CurrentPageNumber_WhenAssignedNonPositive_ClampsToOne()
    {
        // Arrange
        var inner = new RecordingTableSource(totalRows: 95);
        using var source = new PagedTableSource(inner, pageSize: 10);

        // Act
        source.CurrentPageNumber = 0;

        // Assert
        source.CurrentPageNumber.Should().Be(1);
    }

    [Theory]
    [InlineData((long)int.MaxValue + 1)]
    [InlineData((long)int.MaxValue + 100)]
    [InlineData(5_000_000_000L)]
    public void Indexer_OnLastWindowWithDefaultPageSize_PassesLongAbsoluteRowToInner(long totalRows)
    {
        // Arrange
        var inner = new RecordingTableSource(totalRows);
        using var source = new PagedTableSource(inner);
        source.SwitchPageFor(totalRows - 1);

        // Act
        var displayedRows = source.Rows;
        _ = source[displayedRows - 1, 0];

        // Assert
        source.WindowStart.Should().BePositive();
        (source.WindowStart + displayedRows).Should().Be(totalRows);
        inner.RequestedRows.Should().ContainSingle().Which.Should().Be(totalRows - 1);
    }

    [Fact]
    public void Indexer_WithRowBeyondIntMaxAndSmallPageSize_PassesLongAbsoluteRowToInner()
    {
        // Arrange
        var firstRowPastIntMax = (long)int.MaxValue + 1;
        var inner = new RecordingTableSource(firstRowPastIntMax + 2);
        using var source = new PagedTableSource(inner, pageSize: 10);
        source.SwitchPageFor(firstRowPastIntMax);

        // Act
        _ = source[(int)(firstRowPastIntMax - source.WindowStart), 0];

        // Assert
        inner.RequestedRows.Should().ContainSingle().Which.Should().Be(firstRowPastIntMax);
    }

    [Fact]
    public void CurrentPageNumber_WithTotalRowsNearLongMaxValue_ClampsWithoutOverflow()
    {
        // Arrange
        var inner = new RecordingTableSource(long.MaxValue);
        using var source = new PagedTableSource(inner, pageSize: 3);

        // Act
        source.CurrentPageNumber = long.MaxValue;
        var displayedRows = source.Rows;
        _ = source[displayedRows - 1, 0];

        // Assert — ceil(long.MaxValue / 3) pages; the window is the last two pages (3 rows + a 1-row remainder)
        var lastPage = ((long.MaxValue - 1) / 3) + 1;
        source.CurrentPageNumber.Should().Be(lastPage);
        source.WindowStart.Should().Be((lastPage - 2) * 3);
        displayedRows.Should().Be(4);
        inner.RequestedRows.Should().ContainSingle().Which.Should().Be(long.MaxValue - 1);
    }

    [Fact]
    public void Rows_OnLastPageWithPageSizeOneAndLongMaxValueRows_ShowsTwoRowsAndPassesLongRowToInner()
    {
        // Arrange
        var inner = new RecordingTableSource(long.MaxValue);
        using var source = new PagedTableSource(inner, pageSize: 1);
        source.SwitchPageFor(long.MaxValue - 1);

        // Act
        var displayedRows = source.Rows;
        _ = source[displayedRows - 1, 0];

        // Assert
        source.CurrentPageNumber.Should().Be(long.MaxValue);
        displayedRows.Should().Be(2);
        source.WindowStart.Should().Be(long.MaxValue - 2);
        inner.RequestedRows.Should().ContainSingle().Which.Should().Be(long.MaxValue - 1);
    }

    [Fact]
    public void Dispose_CalledTwice_DisposesInnerSourceOnce()
    {
        // Arrange
        var inner = new DisposableTableSource();
        var source = new PagedTableSource(inner);
        source.Dispose();

        // Act
        source.Dispose();

        // Assert
        inner.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_DisposesInnerSource()
    {
        // Arrange
        var inner = new DisposableTableSource();
        var source = new PagedTableSource(inner);

        // Act
        source.Dispose();

        // Assert
        inner.IsDisposed.Should().BeTrue();
    }
}

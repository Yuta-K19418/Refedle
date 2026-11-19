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

    [Fact]
    public void Constructor_WithNullInner_ThrowsArgumentNullException()
    {
        // Arrange / Act
        Func<PagedTableSource> act = () => new PagedTableSource(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositivePageSize_ThrowsArgumentOutOfRangeException(int pageSize)
    {
        // Arrange
        RecordingTableSource inner = new(totalRows: 10);

        // Act
        Func<PagedTableSource> act = () => new PagedTableSource(inner, pageSize);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithPageSizeAboveOneThirdOfIntMaxValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange — three pages of this size would no longer fit in int.MaxValue
        RecordingTableSource inner = new(totalRows: 10);

        // Act
        Func<PagedTableSource> act = () => new PagedTableSource(inner, int.MaxValue / 3 + 1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rows_OnFirstPage_ShowsTwoPages()
    {
        // Arrange — 10 rows per page, 100 rows total; page 1 has no previous page
        RecordingTableSource inner = new(totalRows: 100);
        using PagedTableSource source = new(inner, pageSize: 10);

        // Act
        int rows = source.Rows;

        // Assert
        rows.Should().Be(20);
        source.WindowStart.Should().Be(0);
    }

    [Fact]
    public void Rows_OnMiddlePage_ShowsThreePages()
    {
        // Arrange
        RecordingTableSource inner = new(totalRows: 100);
        using PagedTableSource source = new(inner, pageSize: 10);
        source.CurrentPageNumber = 3;

        // Act
        int rows = source.Rows;

        // Assert
        rows.Should().Be(30);
        source.WindowStart.Should().Be(10);
    }

    [Fact]
    public void Rows_OnLastPage_ShowsRemainingRowsOnly()
    {
        // Arrange — 95 rows = 9 full pages + 5 rows; page 10 shows pages 9-10
        RecordingTableSource inner = new(totalRows: 95);
        using PagedTableSource source = new(inner, pageSize: 10);
        source.CurrentPageNumber = 10;

        // Act
        int rows = source.Rows;

        // Assert
        rows.Should().Be(15);
        source.WindowStart.Should().Be(80);
    }

    [Fact]
    public void Rows_WhenTableSmallerThanOnePage_ShowsAllRows()
    {
        // Arrange
        RecordingTableSource inner = new(totalRows: 7);
        using PagedTableSource source = new(inner, pageSize: 10);

        // Act
        int rows = source.Rows;

        // Assert
        rows.Should().Be(7);
    }

    [Fact]
    public void Rows_WhenTotalRowsIsZero_ShowsZeroRows()
    {
        // Arrange
        RecordingTableSource inner = new(totalRows: 0);
        using PagedTableSource source = new(inner, pageSize: 10);

        // Act
        int rows = source.Rows;

        // Assert
        rows.Should().Be(0);
    }

    [Fact]
    public void Rows_AfterTotalRowsGrows_ExtendsWindowWithoutMovingIt()
    {
        // Arrange — background indexing keeps appending rows while the user stays put
        RecordingTableSource inner = new(totalRows: 45);
        using PagedTableSource source = new(inner, pageSize: 10);
        source.CurrentPageNumber = 5;

        // Act
        inner.TotalRows = 80;
        int rows = source.Rows;

        // Assert
        source.WindowStart.Should().Be(30);
        rows.Should().Be(30);
    }

    [Fact]
    public void Indexer_OnMiddlePage_ReadsInnerRowAtWindowOffset()
    {
        // Arrange — page 3's window covers pages 2-4, so it starts at absolute row 10
        RecordingTableSource inner = new(totalRows: 100);
        using PagedTableSource source = new(inner, pageSize: 10);
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
        RecordingTableSource inner = new(totalRows: 10);
        using PagedTableSource source = new(inner, pageSize: 10);

        // Act
        string[] columnNames = source.ColumnNames;
        int columns = source.Columns;

        // Assert
        columnNames.Should().Equal(["value"]);
        columns.Should().Be(1);
    }

    [Fact]
    public void Inner_ExposesWrappedSource()
    {
        // Arrange
        RecordingTableSource inner = new(totalRows: 10);
        using PagedTableSource source = new(inner, pageSize: 10);

        // Act
        IExtendedTableSource recovered = source.Inner;

        // Assert
        recovered.Should().BeSameAs(inner);
    }

    [Fact]
    public void SwitchPageFor_RowInNextPage_ShiftsWindowForwardByPage()
    {
        // Arrange — page 3's window covers pages 2-4 (start row 10); row 40 opens page 5
        RecordingTableSource inner = new(totalRows: 100);
        using PagedTableSource source = new(inner, pageSize: 10);
        source.CurrentPageNumber = 3;

        // Act
        long windowShift = source.SwitchPageFor(40);

        // Assert — the window slides to pages 4-6, shifting its start by two pages
        windowShift.Should().Be(20);
        source.CurrentPageNumber.Should().Be(5);
        source.WindowStart.Should().Be(30);
    }

    [Fact]
    public void SwitchPageFor_RowOnCurrentPage_ReturnsZeroShift()
    {
        // Arrange
        RecordingTableSource inner = new(totalRows: 100);
        using PagedTableSource source = new(inner, pageSize: 10);
        source.CurrentPageNumber = 3;

        // Act
        long windowShift = source.SwitchPageFor(25);

        // Assert
        windowShift.Should().Be(0);
        source.CurrentPageNumber.Should().Be(3);
    }

    [Fact]
    public void SwitchPageFor_RowOnAdjacentPageInsideWindow_ReturnsZeroShiftButUpdatesPage()
    {
        // Arrange — page 1 window spans pages 1-2; row 12 opens page 2, window stays
        RecordingTableSource inner = new(totalRows: 100);
        using PagedTableSource source = new(inner, pageSize: 10);

        // Act
        long windowShift = source.SwitchPageFor(12);

        // Assert
        windowShift.Should().Be(0);
        source.CurrentPageNumber.Should().Be(2);
        source.WindowStart.Should().Be(0);
    }

    [Fact]
    public void SwitchPageFor_RowBeyondEnd_ClampsToLastPage()
    {
        // Arrange
        RecordingTableSource inner = new(totalRows: 95);
        using PagedTableSource source = new(inner, pageSize: 10);

        // Act
        long windowShift = source.SwitchPageFor(94);

        // Assert
        source.CurrentPageNumber.Should().Be(10);
        source.WindowStart.Should().Be(80);
        windowShift.Should().Be(80);
    }

    [Fact]
    public void CurrentPageNumber_WhenAssignedBeyondLastPage_ClampsToPageCount()
    {
        // Arrange
        RecordingTableSource inner = new(totalRows: 95);
        using PagedTableSource source = new(inner, pageSize: 10);

        // Act
        source.CurrentPageNumber = 99;

        // Assert
        source.CurrentPageNumber.Should().Be(10);
    }

    [Fact]
    public void CurrentPageNumber_WhenAssignedNonPositive_ClampsToOne()
    {
        // Arrange
        RecordingTableSource inner = new(totalRows: 95);
        using PagedTableSource source = new(inner, pageSize: 10);

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
        RecordingTableSource inner = new(totalRows);
        using PagedTableSource source = new(inner);
        source.SwitchPageFor(totalRows - 1);

        // Act
        int displayedRows = source.Rows;
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
        long firstRowPastIntMax = (long)int.MaxValue + 1;
        RecordingTableSource inner = new(firstRowPastIntMax + 2);
        using PagedTableSource source = new(inner, pageSize: 10);
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
        RecordingTableSource inner = new(long.MaxValue);
        using PagedTableSource source = new(inner, pageSize: 3);

        // Act
        source.CurrentPageNumber = long.MaxValue;
        int displayedRows = source.Rows;
        _ = source[displayedRows - 1, 0];

        // Assert — ceil(long.MaxValue / 3) pages; the window is the last two pages (3 rows + a 1-row remainder)
        long lastPage = ((long.MaxValue - 1) / 3) + 1;
        source.CurrentPageNumber.Should().Be(lastPage);
        source.WindowStart.Should().Be((lastPage - 2) * 3);
        displayedRows.Should().Be(4);
        inner.RequestedRows.Should().ContainSingle().Which.Should().Be(long.MaxValue - 1);
    }

    [Fact]
    public void Rows_OnLastPageWithPageSizeOneAndLongMaxValueRows_ShowsTwoRowsAndPassesLongRowToInner()
    {
        // Arrange
        RecordingTableSource inner = new(long.MaxValue);
        using PagedTableSource source = new(inner, pageSize: 1);
        source.SwitchPageFor(long.MaxValue - 1);

        // Act
        int displayedRows = source.Rows;
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
        EmptyCountingDisposableTableSource inner = new();
        PagedTableSource source = new(inner);
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
        EmptyCountingDisposableTableSource inner = new();
        PagedTableSource source = new(inner);

        // Act
        source.Dispose();

        // Assert
        inner.IsDisposed.Should().BeTrue();
    }
}

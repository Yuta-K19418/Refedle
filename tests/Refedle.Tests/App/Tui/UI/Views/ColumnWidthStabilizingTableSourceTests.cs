using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Terminal.Gui.Text;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed class ColumnWidthStabilizingTableSourceTests
{
    private sealed class MutableFakeTableSource(
        string[] columnNames,
        string[] rawColumnNames,
        IReadOnlyList<IReadOnlyList<object>> rows) : IExtendedTableSource
    {
        private string[] _columnNames = columnNames;
        private string[] _rawColumnNames = rawColumnNames;
        private IReadOnlyList<IReadOnlyList<object>> _rows = rows;

        public long TotalRows => _rows.Count;
        public int Columns => _columnNames.Length;
        public string[] ColumnNames => _columnNames;
        public string[] RawColumnNames => _rawColumnNames;
        public object this[long row, int col] => _rows[(int)row][col];

        public void Replace(string[] newColumnNames, string[] newRawColumnNames, IReadOnlyList<IReadOnlyList<object>> newRows)
        {
            _columnNames = newColumnNames;
            _rawColumnNames = newRawColumnNames;
            _rows = newRows;
        }
    }

    private sealed class CountingDisposableFakeTableSource : IExtendedTableSource, IDisposable
    {
        public int DisposeCount { get; private set; }
        public long TotalRows => 0;
        public int Columns => 0;
        public string[] ColumnNames => [];
        public string[] RawColumnNames => [];
        public object this[long row, int col] => throw new NotImplementedException();
        public void Dispose() => DisposeCount++;
    }

    [Fact]
    public void Constructor_SetsInitialMinWidthToHeaderLength()
    {
        // Arrange
        ObjectRowsFakeTableSource inner = new(["short", "muchLongerHeader"], []);
        TableStyle style = new();

        // Act
        using ColumnWidthStabilizingTableSource source = new(inner, style);

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be("short".Length);
        style.ColumnStyles[1].MinWidth.Should().Be("muchLongerHeader".Length);
    }

    [Fact]
    public void Constructor_ExistingMinWidthGreaterThanHeader_PreservesExistingMinWidth()
    {
        // Arrange
        ObjectRowsFakeTableSource inner = new(["a"], []);
        TableStyle style = new();
        style.GetOrCreateColumnStyle(0).MinWidth = 20;

        // Act
        using ColumnWidthStabilizingTableSource source = new(inner, style);

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be(20);
    }

    [Fact]
    public void Constructor_HeaderWiderThanConfiguredMaximum_ClampsInitialMinWidth()
    {
        // Arrange
        ObjectRowsFakeTableSource inner = new([new string('h', 20)], []);
        TableStyle style = new();
        style.GetOrCreateColumnStyle(0).MaxWidth = 10;

        // Act
        using ColumnWidthStabilizingTableSource source = new(inner, style);

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be(10);
    }

    [Fact]
    public void Constructor_ZeroColumnSource_CreatesNoColumnStylesAndDisposesSafely()
    {
        // Arrange
        ObjectRowsFakeTableSource inner = new([], []);
        TableStyle style = new();
        using ColumnWidthStabilizingTableSource source = new(inner, style);

        // Act
        Action act = () => source.Dispose();

        // Assert
        style.ColumnStyles.Should().BeEmpty();
        act.Should().NotThrow();
    }

    [Fact]
    public void Indexer_ObservingLongerValue_GrowsMinWidth()
    {
        // Arrange
        IReadOnlyList<IReadOnlyList<object>> rows = [["a"], ["muchLongerValue"]];
        ObjectRowsFakeTableSource inner = new(["col"], rows);
        TableStyle style = new();
        using ColumnWidthStabilizingTableSource source = new(inner, style);

        // Act
        _ = source[1, 0];

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be("muchLongerValue".Length);
    }

    [Fact]
    public void Indexer_ObservingShorterValueAfterLonger_DoesNotShrinkMinWidth()
    {
        // Arrange
        IReadOnlyList<IReadOnlyList<object>> rows = [["muchLongerValue"], ["a"]];
        ObjectRowsFakeTableSource inner = new(["col"], rows);
        TableStyle style = new();
        using ColumnWidthStabilizingTableSource source = new(inner, style);
        _ = source[0, 0];

        // Act
        _ = source[1, 0];

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be("muchLongerValue".Length);
    }

    [Fact]
    public void Indexer_ValueWiderThanConfiguredMaximum_ClampsStableMinimumWidth()
    {
        // Arrange
        IReadOnlyList<IReadOnlyList<object>> rows = [[new string('x', 11)]];
        ObjectRowsFakeTableSource inner = new(["col"], rows);
        TableStyle style = new();
        style.GetOrCreateColumnStyle(0).MaxWidth = 10;
        using ColumnWidthStabilizingTableSource source = new(inner, style);

        // Act
        _ = source[0, 0];

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be(10);
    }

    [Fact]
    public void Indexer_ReturnsInnerSourceValue()
    {
        // Arrange
        IReadOnlyList<IReadOnlyList<object>> rows = [["value"]];
        ObjectRowsFakeTableSource inner = new(["col"], rows);
        TableStyle style = new();
        using ColumnWidthStabilizingTableSource source = new(inner, style);

        // Act
        object value = source[0, 0];

        // Assert
        value.Should().Be("value");
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("value", 5)]
    [InlineData(42, 2)]
    [InlineData(3.5, 3)]
    public void Indexer_VariousCellValueTypes_TracksDisplayWidthWithoutThrowing(object cellValue, int expectedWidth)
    {
        // Arrange
        IReadOnlyList<IReadOnlyList<object>> rows = [[cellValue]];
        ObjectRowsFakeTableSource inner = new([""], rows);
        TableStyle style = new();
        using ColumnWidthStabilizingTableSource source = new(inner, style);

        // Act
        _ = source[0, 0];

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be(expectedWidth);
    }

    [Fact]
    public void Constructor_WideCjkHeader_TracksDisplayColumnsNotUtf16Length()
    {
        // Arrange — 2 CJK characters occupy 4 terminal columns but only 2 UTF-16 code units.
        ObjectRowsFakeTableSource inner = new(["名前"], []);
        TableStyle style = new();

        // Act
        using ColumnWidthStabilizingTableSource source = new(inner, style);

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be("名前".GetColumns());
        style.ColumnStyles[0].MinWidth.Should().NotBe("名前".Length);
    }

    [Fact]
    public void Indexer_WideCjkCellValue_GrowsMinWidthByDisplayColumnsNotUtf16Length()
    {
        // Arrange
        const string value = "日本語テスト";
        IReadOnlyList<IReadOnlyList<object>> rows = [[value]];
        ObjectRowsFakeTableSource inner = new(["col"], rows);
        TableStyle style = new();
        using ColumnWidthStabilizingTableSource source = new(inner, style);

        // Act
        _ = source[0, 0];

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be(value.GetColumns());
        style.ColumnStyles[0].MinWidth.Should().NotBe(value.Length);
    }

    [Fact]
    public void Indexer_WhenInnerSourceAddsColumn_TracksNewColumnWithoutThrowing()
    {
        // Arrange
        MutableFakeTableSource inner = new(["first"], ["first"], [["a"]]);
        TableStyle style = new();
        using ColumnWidthStabilizingTableSource source = new(inner, style);
        inner.Replace(["first", "second"], ["first", "second"], [["a", "expanded"]]);

        // Act
        Func<object> act = () => _ = source[0, 1];

        // Assert
        act.Should().NotThrow();
        style.ColumnStyles[1].MinWidth.Should().Be("expanded".Length);
    }

    [Fact]
    public void RawColumnNames_WhenInnerSourceGrows_ReturnsCurrentDistinctRawNames()
    {
        // Arrange
        MutableFakeTableSource inner = new(["first (text)"], ["first"], [["a"]]);
        TableStyle style = new();
        using ColumnWidthStabilizingTableSource source = new(inner, style);
        inner.Replace(["first (text)", "second (number)"], ["first", "second"], [["a", "2"]]);

        // Act
        string[] rawColumnNames = source.RawColumnNames;

        // Assert
        rawColumnNames.Should().Equal("first", "second");
    }

    [Fact]
    public void RowsColumnsColumnNames_DelegateToInner()
    {
        // Arrange
        IReadOnlyList<IReadOnlyList<object>> rows = [["v1", "v2"], ["v3", "v4"], ["v5", "v6"]];
        ObjectRowsFakeTableSource inner = new(["a", "b"], rows);
        TableStyle style = new();
        using ColumnWidthStabilizingTableSource source = new(inner, style);

        // Act
        long rowCount = source.TotalRows;
        int columnCount = source.Columns;
        string[] columnNames = source.ColumnNames;

        // Assert
        rowCount.Should().Be(3);
        columnCount.Should().Be(2);
        columnNames.Should().Equal("a", "b");
    }

    [Fact]
    public void Dispose_DisposesInnerSourceWhenDisposable()
    {
        // Arrange — source is the sole owner of inner; it is not independently wrapped in a
        // using so this test's single Dispose() call is the only disposal that occurs.
        EmptyFlagDisposableTableSource inner = new();
        TableStyle style = new();
        ColumnWidthStabilizingTableSource source = new(inner, style);

        // Act
        source.Dispose();

        // Assert
        inner.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenInnerIsNotDisposable()
    {
        // Arrange
        ObjectRowsFakeTableSource inner = new(["col"], []);
        TableStyle style = new();
        using ColumnWidthStabilizingTableSource source = new(inner, style);

        // Act
        Action act = () => source.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DisposesInnerSourceExactlyOnce()
    {
        // Arrange — source is the sole owner of inner; it is not independently wrapped in a
        // using so the two explicit Dispose() calls below are the only disposals that occur.
        CountingDisposableFakeTableSource inner = new();
        TableStyle style = new();
        ColumnWidthStabilizingTableSource source = new(inner, style);

        // Act
        source.Dispose();
        source.Dispose();

        // Assert
        inner.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task Indexer_ConcurrentAccessUnderContention_ConvergesToTrueMaximumWidth()
    {
        // Arrange — a barrier races every task for the lock at once, stress-testing convergence
        // under contention. It does not force a specific interleaving: the lock already makes the
        // observed-width comparison and the MinWidth write one atomic section.
        int[] widths = [50, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        IReadOnlyList<IReadOnlyList<object>> rows = [.. widths.Select(w => (IReadOnlyList<object>)[new string('x', w)])];
        ObjectRowsFakeTableSource inner = new(["col"], rows);
        TableStyle style = new();
        using ColumnWidthStabilizingTableSource source = new(inner, style);
        using Barrier barrier = new(rows.Count);
        // LongRunning gives each blocked task its own thread instead of draining the thread pool.
        IEnumerable<Task> accesses = Enumerable.Range(0, rows.Count).Select(i => Task.Factory.StartNew(
            () =>
            {
                barrier.SignalAndWait();
                _ = source[i, 0];
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default));

        // Act
        await Task.WhenAll(accesses);

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be(50);
    }
}

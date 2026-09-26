using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views;
using Terminal.Gui.Text;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.Ui.Views;

public sealed class ColumnWidthStabilizingTableSourceTests
{
    private sealed class FakeTableSource(string[] columnNames, IReadOnlyList<IReadOnlyList<object>> rows) : ITableSource
    {
        public int Rows => rows.Count;
        public int Columns => columnNames.Length;
        public string[] ColumnNames => columnNames;
        public object this[int row, int col] => rows[row][col];
    }

    private sealed class MutableFakeTableSource(string[] columnNames, IReadOnlyList<IReadOnlyList<object>> rows) : ITableSource
    {
        private string[] _columnNames = columnNames;
        private IReadOnlyList<IReadOnlyList<object>> _rows = rows;

        public int Rows => _rows.Count;
        public int Columns => _columnNames.Length;
        public string[] ColumnNames => _columnNames;
        public object this[int row, int col] => _rows[row][col];

        public void Replace(string[] newColumnNames, IReadOnlyList<IReadOnlyList<object>> newRows)
        {
            _columnNames = newColumnNames;
            _rows = newRows;
        }
    }

    private sealed class DisposableFakeTableSource : ITableSource, IDisposable
    {
        public bool IsDisposed { get; private set; }
        public int Rows => 0;
        public int Columns => 0;
        public string[] ColumnNames => [];
        public object this[int row, int col] => throw new NotImplementedException();
        public void Dispose() => IsDisposed = true;
    }

    private sealed class CountingDisposableFakeTableSource : ITableSource, IDisposable
    {
        public int DisposeCount { get; private set; }
        public int Rows => 0;
        public int Columns => 0;
        public string[] ColumnNames => [];
        public object this[int row, int col] => throw new NotImplementedException();
        public void Dispose() => DisposeCount++;
    }

    [Fact]
    public void Constructor_SetsInitialMinWidthToHeaderLength()
    {
        // Arrange
        var inner = new FakeTableSource(["short", "muchLongerHeader"], []);
        var style = new TableStyle();

        // Act
        using var source = new ColumnWidthStabilizingTableSource(inner, style);

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be("short".Length);
        style.ColumnStyles[1].MinWidth.Should().Be("muchLongerHeader".Length);
    }

    [Fact]
    public void Constructor_ExistingMinWidthGreaterThanHeader_PreservesExistingMinWidth()
    {
        // Arrange
        var inner = new FakeTableSource(["a"], []);
        var style = new TableStyle();
        style.GetOrCreateColumnStyle(0).MinWidth = 20;

        // Act
        using var source = new ColumnWidthStabilizingTableSource(inner, style);

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be(20);
    }

    [Fact]
    public void Constructor_HeaderWiderThanConfiguredMaximum_ClampsInitialMinWidth()
    {
        // Arrange
        var inner = new FakeTableSource([new string('h', 20)], []);
        var style = new TableStyle();
        style.GetOrCreateColumnStyle(0).MaxWidth = 10;

        // Act
        using var source = new ColumnWidthStabilizingTableSource(inner, style);

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be(10);
    }

    [Fact]
    public void Constructor_ZeroColumnSource_CreatesNoColumnStylesAndDisposesSafely()
    {
        // Arrange
        var inner = new FakeTableSource([], []);
        var style = new TableStyle();
        using var source = new ColumnWidthStabilizingTableSource(inner, style);

        // Act
        var act = () => source.Dispose();

        // Assert
        style.ColumnStyles.Should().BeEmpty();
        act.Should().NotThrow();
    }

    [Fact]
    public void Indexer_ObservingLongerValue_GrowsMinWidth()
    {
        // Arrange
        IReadOnlyList<IReadOnlyList<object>> rows = [["a"], ["muchLongerValue"]];
        var inner = new FakeTableSource(["col"], rows);
        var style = new TableStyle();
        using var source = new ColumnWidthStabilizingTableSource(inner, style);

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
        var inner = new FakeTableSource(["col"], rows);
        var style = new TableStyle();
        using var source = new ColumnWidthStabilizingTableSource(inner, style);
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
        var inner = new FakeTableSource(["col"], rows);
        var style = new TableStyle();
        style.GetOrCreateColumnStyle(0).MaxWidth = 10;
        using var source = new ColumnWidthStabilizingTableSource(inner, style);

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
        var inner = new FakeTableSource(["col"], rows);
        var style = new TableStyle();
        using var source = new ColumnWidthStabilizingTableSource(inner, style);

        // Act
        var value = source[0, 0];

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
        var inner = new FakeTableSource([""], rows);
        var style = new TableStyle();
        using var source = new ColumnWidthStabilizingTableSource(inner, style);

        // Act
        _ = source[0, 0];

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be(expectedWidth);
    }

    [Fact]
    public void Constructor_WideCjkHeader_TracksDisplayColumnsNotUtf16Length()
    {
        // Arrange — 2 CJK characters occupy 4 terminal columns but only 2 UTF-16 code units.
        var inner = new FakeTableSource(["名前"], []);
        var style = new TableStyle();

        // Act
        using var source = new ColumnWidthStabilizingTableSource(inner, style);

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
        var inner = new FakeTableSource(["col"], rows);
        var style = new TableStyle();
        using var source = new ColumnWidthStabilizingTableSource(inner, style);

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
        var inner = new MutableFakeTableSource(["first"], [["a"]]);
        var style = new TableStyle();
        using var source = new ColumnWidthStabilizingTableSource(inner, style);
        inner.Replace(["first", "second"], [["a", "expanded"]]);

        // Act
        var act = () => _ = source[0, 1];

        // Assert
        act.Should().NotThrow();
        style.ColumnStyles[1].MinWidth.Should().Be("expanded".Length);
    }

    [Fact]
    public void RowsColumnsColumnNames_DelegateToInner()
    {
        // Arrange
        IReadOnlyList<IReadOnlyList<object>> rows = [["v1", "v2"], ["v3", "v4"], ["v5", "v6"]];
        var inner = new FakeTableSource(["a", "b"], rows);
        var style = new TableStyle();
        using var source = new ColumnWidthStabilizingTableSource(inner, style);

        // Act
        var rowCount = source.Rows;
        var columnCount = source.Columns;
        var columnNames = source.ColumnNames;

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
        var inner = new DisposableFakeTableSource();
        var style = new TableStyle();
        var source = new ColumnWidthStabilizingTableSource(inner, style);

        // Act
        source.Dispose();

        // Assert
        inner.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenInnerIsNotDisposable()
    {
        // Arrange
        var inner = new FakeTableSource(["col"], []);
        var style = new TableStyle();
        using var source = new ColumnWidthStabilizingTableSource(inner, style);

        // Act
        var act = () => source.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DisposesInnerSourceExactlyOnce()
    {
        // Arrange — source is the sole owner of inner; it is not independently wrapped in a
        // using so the two explicit Dispose() calls below are the only disposals that occur.
        var inner = new CountingDisposableFakeTableSource();
        var style = new TableStyle();
        var source = new ColumnWidthStabilizingTableSource(inner, style);

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
        var widths = new[] { 50, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        IReadOnlyList<IReadOnlyList<object>> rows = [.. widths.Select(w => (IReadOnlyList<object>)[new string('x', w)])];
        var inner = new FakeTableSource(["col"], rows);
        var style = new TableStyle();
        using var source = new ColumnWidthStabilizingTableSource(inner, style);
        using var barrier = new Barrier(rows.Count);
        var accesses = Enumerable.Range(0, rows.Count).Select(i => Task.Run(() =>
        {
            barrier.SignalAndWait();
            _ = source[i, 0];
        }));

        // Act
        await Task.WhenAll(accesses);

        // Assert
        style.ColumnStyles[0].MinWidth.Should().Be(50);
    }
}

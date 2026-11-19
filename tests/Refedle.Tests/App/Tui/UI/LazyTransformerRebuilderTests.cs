using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui.UI;
using Refedle.TestHelpers.IO;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

public sealed class LazyTransformerRebuilderTests
{
    private static readonly IReadOnlyList<IMorphAction> RenameActions =
        [new RenameColumnAction { OldName = "col1", NewName = "renamed" }];

    [Fact]
    public void Rebuild_WithSelectedCell_ReplacesTableAndKeepsSelection()
    {
        // Arrange
        using Fixture fixture = new(totalRows: 2, "col1", "col2");
        ITableSource? oldTable = fixture.View.Table;
        fixture.View.SetSelection(1, 0, false);

        // Act
        fixture.Rebuild("col1", "col2", "col3");

        // Assert
        fixture.View.Table.Should().NotBeSameAs(oldTable);
        fixture.View.Table.Should().NotBeNull().And.Subject.As<ITableSource>().Columns.Should().Be(3);
        fixture.Selection.SelectedCell.Should().Be(new System.Drawing.Point(1, 0));
    }

    [Fact]
    public void Rebuild_WithScrolledView_KeepsRowOffsetAndSelection()
    {
        // Arrange
        using Fixture fixture = new(totalRows: 10, "col1", "col2");
        ITableSource? oldTable = fixture.View.Table;
        fixture.View.SetSelection(1, 3, false);
        fixture.View.RowOffset = 2;

        // Act
        fixture.Rebuild("col1", "col2", "col3");

        // Assert
        fixture.View.Table.Should().NotBeSameAs(oldTable);
        fixture.Selection.SelectedCell.Should().Be(new System.Drawing.Point(1, 3));
        fixture.View.RowOffset.Should().Be(2);
    }

    [Fact]
    public void Rebuild_WithLaterPageSelected_KeepsPageAndSelection()
    {
        // Arrange
        using Fixture fixture = new(totalRows: 1_500_000_000, "col1", "col2");
        int pageSize = PagedTableSource.DefaultPageSize;
        fixture.View.SetSelection(1, pageSize + 5, false);

        // Act
        fixture.Rebuild("col1", "col2", "col3");

        // Assert
        PagedTableSource paged = fixture.View.Table.Should().BeOfType<PagedTableSource>().Subject;
        paged.CurrentPageNumber.Should().Be(2);
        fixture.Selection.SelectedCell.Should().Be(new System.Drawing.Point(1, pageSize + 5));
    }

    [Fact]
    public void Rebuild_WithEmptyTable_ReplacesTableWithoutSelection()
    {
        // Arrange
        using Fixture fixture = new(totalRows: 0, "col1");
        ITableSource? oldTable = fixture.View.Table;

        // Act
        fixture.Rebuild("col1", "col2");

        // Assert
        fixture.View.Table.Should().NotBeSameAs(oldTable);
        fixture.View.Table.Should().NotBeNull().And.Subject.As<ITableSource>().Columns.Should().Be(2);
        fixture.View.Value.Should().BeNull();
    }

    [Fact]
    public void Rebuild_WhenColumnsShrink_ClampsSelectedColumn()
    {
        // Arrange
        using Fixture fixture = new(totalRows: 2, "col1", "col2", "col3");
        fixture.View.SetSelection(2, 0, false);

        // Act
        fixture.Rebuild("col1", "col2");

        // Assert
        fixture.Selection.SelectedCell.Should().Be(new System.Drawing.Point(1, 0));
    }

    [Fact]
    public void Rebuild_WhenViewIsNotCurrent_LeavesTableUntouched()
    {
        // Arrange
        using Fixture fixture = new(totalRows: 2, "col1");
        ITableSource? oldTable = fixture.View.Table;
        fixture.CurrentView = null;

        // Act
        fixture.Rebuild("col1", "col2");

        // Assert
        fixture.View.Table.Should().BeSameAs(oldTable);
    }

    [Fact]
    public void Rebuild_WhenTableIsNotPaged_LeavesTableUntouched()
    {
        // Arrange
        using Fixture fixture = new(totalRows: 2, "col1");
        using System.Data.DataTable dataTable = new();
        DataTableSource unpaged = new(dataTable);
        fixture.View.Table = unpaged;

        // Act
        fixture.Rebuild("col1", "col2");

        // Assert
        fixture.View.Table.Should().BeSameAs(unpaged);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly IApplication _app = TestAppFactory.Create();
        private readonly TempFile _file = TempFile.ReservePath(".jsonl");
        private readonly AppState _state = new();
        private readonly LazyTransformerRebuilder _rebuilder;
        private readonly RowByteCache _cache;
        private readonly JsonLinesTableSource _source;
        private readonly LazyTransformer _lazy;

#pragma warning disable CA2000 // The paged source is owned by the view and disposed with it
        public Fixture(long totalRows, params string[] columns)
        {
            File.WriteAllText(_file.Path, "{\"col1\": \"a\", \"col2\": \"b\"}\n{\"col1\": \"x\", \"col2\": \"y\"}\n");
            _state.SetActionStack(RenameActions);
            TableSchema schema = CreateSchema(columns);
            _cache = new RowByteCache(new CompletedStubRowIndexer(_file.Path, totalRows));
            _source = new JsonLinesTableSource(_cache, schema);
            _lazy = LazyTransformer.Create(_source, schema, RenameActions);
            TableStyle style = new() { AlwaysShowHeaders = true };
            View = new JsonLinesTableView
            {
                Table = new PagedTableSource(new ColumnWidthStabilizingTableSource(_lazy, style)),
                Style = style,
            };
            CurrentView = View;
            _rebuilder = new LazyTransformerRebuilder(_state, () => CurrentView);
        }
#pragma warning restore CA2000

        public JsonLinesTableView View { get; }
        public View? CurrentView { get; set; }
        public TableSelection Selection => View.Value.Should().BeOfType<TableSelection>().Subject;

        public void Rebuild(params string[] columns)
        {
            TableSchema refined = CreateSchema(columns);
            _source.UpdateSchema(refined);
            _rebuilder.Rebuild(View, _source, _lazy, refined);
        }

        public void Dispose()
        {
            View.Dispose();
            _lazy.Dispose();
            _source.Dispose();
            _cache.Dispose();
            _state.Dispose();
            _app.Dispose();
            _file.Dispose();
        }

        private static TableSchema CreateSchema(string[] columns) => new()
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [.. columns.Select((name, index) => new ColumnSchema { Name = name, Type = ColumnType.Text, ColumnIndex = index })],
        };
    }
}

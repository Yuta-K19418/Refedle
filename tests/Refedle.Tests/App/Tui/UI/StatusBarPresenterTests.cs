using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui.UI;
using Refedle.TestHelpers.IO;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

public sealed class StatusBarPresenterTests : IDisposable
{
    private readonly TempFileCollection _tempFiles = new();

    public void Dispose() => _tempFiles.Dispose();

    [Fact]
    public void Refresh_WithNoFilePath_UsesDefaultHints()
    {
        // Arrange
        using Fixture fixture = new();

        // Act
        fixture.Presenter.Refresh();

        // Assert
        fixture.Hints().Should().Equal("o:Open", "s:Save", "q:Quit", "?:Help");
    }

    [Fact]
    public void Refresh_WithCsvFilePath_UsesDefaultHints()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.StartNewFile(_tempFiles.Create(".csv", "col1,col2\nvalue1,value2\n"));

        // Act
        fixture.Presenter.Refresh();

        // Assert
        fixture.Hints().Should().Equal("o:Open", "s:Save", "q:Quit", "?:Help");
    }

    [Theory]
    [InlineData(".jsonl", "{\"col1\": \"value\"}\n")]
    [InlineData(".json", "[1,2,3]")]
    public void Refresh_WithJsonLinesOrJsonArrayFile_IncludesToggleHint(string extension, string content)
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.StartNewFile(_tempFiles.Create(extension, content));

        // Act
        fixture.Presenter.Refresh();

        // Assert
        fixture.Hints().Should().Contain("t:Tree/Table");
    }

    [Fact]
    public void Refresh_WithJsonObjectFile_DoesNotIncludeToggleHint()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.StartNewFile(_tempFiles.Create(".json", "{\"id\":1}"));

        // Act
        fixture.Presenter.Refresh();

        // Assert
        fixture.Hints().Should().NotContain("t:Tree/Table");
    }

    [Fact]
    public void Refresh_WithMorphTableViewShown_IncludesMenuHint()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.StartNewFile(_tempFiles.Create(".jsonl", "{\"col1\": \"value\"}\n"));
        using PlainMorphTableView view = new();
        fixture.CurrentView = view;

        // Act
        fixture.Presenter.Refresh();

        // Assert
        fixture.Hints().Should().Contain("x:Menu");
    }

    [Fact]
    public void Refresh_WithNonTableViewShown_DoesNotIncludeMenuHint()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.StartNewFile(_tempFiles.Create(".jsonl", "{\"col1\": \"value\"}\n"));
        using View view = new();
        fixture.CurrentView = view;

        // Act
        fixture.Presenter.Refresh();

        // Assert
        fixture.Hints().Should().NotContain("x:Menu");
    }

    [Fact]
    public void Refresh_WithFocusedTableMode_IncludesBackHintAndNoMenuHint()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.StartNewFile(_tempFiles.Create(".jsonl", "{\"col1\": \"value\"}\n"));
        fixture.State.EnterFocusedTable(CreateDrillDown([]));
        using PlainMorphTableView view = new();
        fixture.CurrentView = view;

        // Act
        fixture.Presenter.Refresh();

        // Assert
        fixture.Hints().Should().Contain("bs:Back").And.NotContain("x:Menu").And.NotContain("t:Tree/Table");
    }

    [Fact]
    public void Refresh_WithBaseActionStack_IncludesClearHint()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.StartNewFile(_tempFiles.Create(".csv", "col1\nval1"));
        fixture.State.SetActionStack([new RenameColumnAction { OldName = "col1", NewName = "renamed" }]);

        // Act
        fixture.Presenter.Refresh();

        // Assert
        fixture.Hints().Should().Contain("c:Clear");
    }

    [Fact]
    public void Refresh_WithDrillDownActionStack_IncludesClearHintEvenWhenBaseStackIsEmpty()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.StartNewFile(_tempFiles.Create(".jsonl", "{\"col1\": \"value\"}\n"));
        fixture.State.EnterFocusedTable(CreateDrillDown([new RenameColumnAction { OldName = "col1", NewName = "renamed" }]));

        // Act
        fixture.Presenter.Refresh();

        // Assert
        fixture.Hints().Should().Contain("c:Clear");
    }

    [Fact]
    public void Refresh_WithoutStatusBar_DoesNotAddItemCountLabel()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using AppState state = new();
        state.CompleteCsvLoad(new CompletedStubRowIndexer("test.csv", 5), CreateSchema());
        using Window window = new();
        StatusBarPresenter presenter = new(window, state, static () => null, static () => false);

        // Act
        presenter.Refresh();

        // Assert
        window.SubViews.OfType<Label>().Should().BeEmpty();
        presenter.GetCurrentStatusBar().Should().BeNull();
    }

    [Fact]
    public void Refresh_WithCompletedIndexer_ShowsFileTotalAsItemCount()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.CompleteCsvLoad(new CompletedStubRowIndexer("test.csv", 5000), CreateSchema());

        // Act
        fixture.Presenter.Refresh();

        // Assert
        fixture.ItemCountTexts().Should().Equal("5000 items");
    }

    [Fact]
    public void Refresh_WithPagedTableShown_ShowsTableTotalInsteadOfFileTotal()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.CompleteCsvLoad(new CompletedStubRowIndexer("test.csv", 5000), CreateSchema());
        using PlainMorphTableView view = new()
        {
            Table = new PagedTableSource(new TenRowTableSource(["col1"], ["col1"])),
        };
        fixture.CurrentView = view;

        // Act
        fixture.Presenter.Refresh();

        // Assert
        fixture.ItemCountTexts().Should().Equal("10 items");
    }

    [Fact]
    public void Refresh_WithIndexerReplacedBetweenCalls_ReplacesCountLabel()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.CompleteCsvLoad(new CompletedStubRowIndexer("test.csv", 1000), CreateSchema());
        fixture.Presenter.Refresh();

        // Act
        fixture.State.CompleteCsvLoad(new CompletedStubRowIndexer("test.csv", 5000), CreateSchema());
        fixture.Presenter.Refresh();

        // Assert
        fixture.ItemCountTexts().Should().Equal("5000 items");
    }

    [Fact]
    public void Refresh_WhenIndexerIsCleared_RemovesExistingCountLabel()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.CompleteCsvLoad(new CompletedStubRowIndexer("test.csv", 5000), CreateSchema());
        fixture.Presenter.Refresh();
        fixture.ItemCountTexts().Should().Equal("5000 items");

        // Act
        fixture.State.BeginJsonObjectLoad();
        fixture.Presenter.Refresh();

        // Assert
        fixture.ItemCountTexts().Should().BeEmpty();
    }

    [Fact]
    public void Refresh_WhileFilterScanRunning_HidesCountLabel()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.CompleteCsvLoad(new CompletedStubRowIndexer("test.csv", 5000), CreateSchema());
        fixture.ScanRunning = true;

        // Act
        fixture.Presenter.Refresh();

        // Assert
        fixture.ItemCountTexts().Should().BeEmpty();
    }

    [Fact]
    public void RemoveItemCountLabel_WithLabelShown_RemovesItFromWindow()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.CompleteCsvLoad(new CompletedStubRowIndexer("test.csv", 5000), CreateSchema());
        fixture.Presenter.Refresh();
        fixture.ItemCountTexts().Should().Equal("5000 items");

        // Act
        fixture.Presenter.RemoveItemCountLabel();

        // Assert
        fixture.ItemCountTexts().Should().BeEmpty();
    }

    [Fact]
    public void RemoveItemCountLabel_WithoutLabel_DoesNotThrow()
    {
        // Arrange
        using Fixture fixture = new();

        // Act
        Action act = fixture.Presenter.RemoveItemCountLabel;

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetCurrentStatusBar_WithStatusBarInWindow_ReturnsIt()
    {
        // Arrange
        using Fixture fixture = new();

        // Act
        StatusBar? result = fixture.Presenter.GetCurrentStatusBar();

        // Assert
        result.Should().BeSameAs(fixture.StatusBar);
    }

    private static TableSchema CreateSchema() => new()
    {
        SourceFormat = DataFormat.Csv,
        Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }],
    };

    private static DrillDownState CreateDrillDown(IReadOnlyList<IMorphAction> actions) => new(
        [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
        new TableSchema
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }],
        },
        ViewMode.JsonLinesTree,
        KeyPath: [],
        ActionStack: actions);

    private sealed class Fixture : IDisposable
    {
        private readonly IApplication _app = TestAppFactory.Create();

        public Fixture()
        {
            StatusBar = new StatusBar();
            Window.Add(StatusBar);
            Presenter = new StatusBarPresenter(Window, State, () => CurrentView, () => ScanRunning);
        }

        public AppState State { get; } = new();
        public Window Window { get; } = new();
        public StatusBar StatusBar { get; }
        public StatusBarPresenter Presenter { get; }
        public View? CurrentView { get; set; }
        public bool ScanRunning { get; set; }

        public IEnumerable<string> Hints() => StatusBar.SubViews.OfType<Shortcut>().Select(static s => s.HelpText);

        public IEnumerable<string> ItemCountTexts() =>
            Window.SubViews.OfType<Label>().Select(static l => l.Text).Where(static t => t.EndsWith(" items", StringComparison.Ordinal));

        public void Dispose()
        {
            Window.Dispose();
            StatusBar.Dispose();
            State.Dispose();
            _app.Dispose();
        }
    }
}

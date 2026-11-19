using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.IO;
using Refedle.Engine.IO.Csv;
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

public sealed class TableViewFactoryTests : IDisposable
{
    private static readonly IReadOnlyList<IMorphAction> RenameActions =
        [new RenameColumnAction { OldName = "col1", NewName = "renamed" }];

    private readonly TempFileCollection _tempFiles = new();

    public void Dispose() => _tempFiles.Dispose();

    [Fact]
    public void CreateCsvTable_WithoutActions_ShowsRawRowsAndSelectsFirstCell()
    {
        // Arrange
        using Fixture fixture = new();
        DataRowIndexer indexer = BuildCsvIndexer();

        // Act
        TableViewResult result = fixture.Factory.CreateCsvTable(indexer, CsvSchema());
        using View view = result.View;

        // Assert
        result.Source.ColumnNames.Should().Equal("col1 (text)", "col2 (text)");
        result.Source[0, 0].Should().Be("value1");
        view.Should().BeOfType<CsvTableView>().Which.Table.Should().BeOfType<PagedTableSource>();
        view.As<MorphTableView>().Value.Should().BeOfType<TableSelection>().Which.SelectedCell.Should().Be(new System.Drawing.Point(0, 0));
    }

    [Fact]
    public void CreateCsvTable_WithActions_ShowsTransformedColumns()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.SetActionStack(RenameActions);
        DataRowIndexer indexer = BuildCsvIndexer();

        // Act
        TableViewResult result = fixture.Factory.CreateCsvTable(indexer, CsvSchema());
        using View view = result.View;

        // Assert
        result.Source.ColumnNames.Should().Equal("renamed (text)", "col2 (text)");
        result.Source[0, 0].Should().Be("value1");
        view.As<MorphTableView>().Table.Should().NotBeNull().And.Subject.As<ITableSource>().ColumnNames.Should().Equal("renamed (text)", "col2 (text)");
    }

    [Fact]
    public void CreateCsvTable_RaisingMorphActionOnView_InvokesMorphCallback()
    {
        // Arrange
        using Fixture fixture = new();
        DataRowIndexer indexer = BuildCsvIndexer();
        using CsvTableView view = fixture.Factory.CreateCsvTable(indexer, CsvSchema()).View.Should().BeOfType<CsvTableView>().Subject;
        Action<IMorphAction>? handler = view.OnMorphAction;
        IMorphAction action = RenameActions[0];

        // Act
        handler.Should().NotBeNull();
        handler.Invoke(action);

        // Assert
        fixture.MorphActions.Should().ContainSingle().Which.Should().BeSameAs(action);
    }

    [Fact]
    public void CreateCsvTable_WithCompletedIndexer_ReportsRowIndexComplete()
    {
        // Arrange
        using Fixture fixture = new();
        DataRowIndexer indexer = BuildCsvIndexer();
        using CsvTableView view = fixture.Factory.CreateCsvTable(indexer, CsvSchema()).View.Should().BeOfType<CsvTableView>().Subject;
        Func<bool>? isComplete = view.IsRowIndexComplete;

        // Act
        isComplete.Should().NotBeNull();
        bool result = isComplete.Invoke();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CreateJsonLinesTable_WithIndexingInProgress_ReportsRowIndexIncomplete()
    {
        // Arrange
        using Fixture fixture = new();
        CheckpointRaisingRowIndexer indexer = new(JsonLinesFile());
        using JsonLinesTableView view = fixture.Factory.CreateJsonLinesTable(indexer, JsonLinesSchema()).View.Should().BeOfType<JsonLinesTableView>().Subject;
        Func<bool>? isComplete = view.IsRowIndexComplete;

        // Act
        isComplete.Should().NotBeNull();
        bool result = isComplete.Invoke();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CreateJsonLinesTable_WithoutActions_ShowsRawColumnsAndRefinedSchemaReachesSource()
    {
        // Arrange
        using Fixture fixture = new();
        string path = JsonLinesFile();
        TableViewResult result = fixture.Factory.CreateJsonLinesTable(new CompletedStubRowIndexer(path, 2), JsonLinesSchema());
        using View view = result.View;
        Action<TableSchema>? onRefined = fixture.State.OnSchemaRefined;
        result.Source.ColumnNames.Should().Equal("col1 (text)", "col2 (text)");

        // Act
        onRefined.Should().NotBeNull();
        onRefined.Invoke(CreateSchema(DataFormat.JsonLines, "col1", "col2", "col3"));

        // Assert
        result.Source.Columns.Should().Be(3);
    }

    [Fact]
    public void CreateJsonLinesTable_WithActions_ShowsTransformedColumns()
    {
        // Arrange
        using Fixture fixture = new();
        fixture.State.SetActionStack(RenameActions);
        string path = JsonLinesFile();

        // Act
        TableViewResult result = fixture.Factory.CreateJsonLinesTable(new CompletedStubRowIndexer(path, 2), JsonLinesSchema());
        using View view = result.View;

        // Assert
        result.Source.ColumnNames.Should().Equal("renamed (text)", "col2 (text)");
    }

    [Fact]
    public void CreateJsonLinesTable_WithRowsAvailable_SelectsFirstCell()
    {
        // Arrange
        using Fixture fixture = new();
        string path = JsonLinesFile();

        // Act
        using View view = fixture.Factory.CreateJsonLinesTable(new CompletedStubRowIndexer(path, 2), JsonLinesSchema()).View;

        // Assert
        view.As<MorphTableView>().Value.Should().BeOfType<TableSelection>().Which.SelectedCell.Should().Be(new System.Drawing.Point(0, 0));
    }

    [Fact]
    public void CreateJsonLinesTable_WithNoRowsYet_SelectsFirstCellWhenFirstCheckpointIsReached()
    {
        // Arrange
        using Fixture fixture = new();
        CheckpointRaisingRowIndexer indexer = new(JsonLinesFile());
        using View view = fixture.Factory.CreateJsonLinesTable(indexer, JsonLinesSchema()).View;
        fixture.CurrentView = view;
        view.As<MorphTableView>().Value.Should().BeNull();

        // Act
        indexer.RaiseFirstCheckpointWithRows(2);
        fixture.RunQueued();

        // Assert
        view.As<MorphTableView>().Value.Should().BeOfType<TableSelection>().Which.SelectedCell.Should().Be(new System.Drawing.Point(0, 0));
    }

    [Fact]
    public void CreateJsonLinesTable_WhenUserMovedSelectionBeforeCheckpoint_KeepsUserSelection()
    {
        // Arrange
        using Fixture fixture = new();
        CheckpointRaisingRowIndexer indexer = new(JsonLinesFile());
        using MorphTableView view = fixture.Factory.CreateJsonLinesTable(indexer, JsonLinesSchema()).View;
        fixture.CurrentView = view;
        indexer.SetTotalRows(2);
        view.SetSelection(0, 1, false);

        // Act
        indexer.RaiseFirstCheckpointWithRows(2);
        fixture.RunQueued();

        // Assert
        view.Value.Should().BeOfType<TableSelection>().Which.SelectedCell.Should().Be(new System.Drawing.Point(0, 1));
    }

    [Fact]
    public void CreateJsonLinesTable_WithNoRowsYet_DispatchesCheckpointHandlingThroughUiThreadInvoke()
    {
        // Arrange
        using Fixture fixture = new();
        CheckpointRaisingRowIndexer indexer = new(JsonLinesFile());
        using View view = fixture.Factory.CreateJsonLinesTable(indexer, JsonLinesSchema()).View;
        fixture.CurrentView = view;

        // Act
        indexer.RaiseFirstCheckpointWithRows(2);

        // Assert
        fixture.Queued.Should().ContainSingle();
        view.As<MorphTableView>().Value.Should().BeNull();
    }

    [Fact]
    public void CreateJsonLinesTable_WhenViewIsReplacedBeforeCheckpoint_DoesNotSelectOnStaleView()
    {
        // Arrange
        using Fixture fixture = new();
        CheckpointRaisingRowIndexer indexer = new(JsonLinesFile());
        using View view = fixture.Factory.CreateJsonLinesTable(indexer, JsonLinesSchema()).View;
        using View other = new();
        fixture.CurrentView = other;

        // Act
        indexer.RaiseFirstCheckpointWithRows(2);
        fixture.RunQueued();

        // Assert
        view.As<MorphTableView>().Value.Should().BeNull();
    }

    [Fact]
    public void CreateJsonLinesTable_WhenCheckpointRaisedTwice_QueuesHandlingOnlyOnce()
    {
        // Arrange
        using Fixture fixture = new();
        CheckpointRaisingRowIndexer indexer = new(JsonLinesFile());
        using View view = fixture.Factory.CreateJsonLinesTable(indexer, JsonLinesSchema()).View;
        fixture.CurrentView = view;

        // Act
        indexer.RaiseFirstCheckpointWithRows(2);
        indexer.RaiseFirstCheckpointWithRows(2);

        // Assert
        fixture.Queued.Should().ContainSingle();
    }

    [Fact]
    public void CreateFocusedSource_WithoutActions_ShowsRawRows()
    {
        // Arrange
        DrillDownState drillDown = CreateDrillDown([]);

        // Act
        IExtendedTableSource source = TableViewFactory.CreateFocusedSource(drillDown);

        // Assert
        source.ColumnNames.Should().Contain("col1 (text)");
        source.TotalRows.Should().Be(1);
    }

    [Fact]
    public void CreateFocusedSource_WithActions_ShowsTransformedColumns()
    {
        // Arrange
        DrillDownState drillDown = CreateDrillDown(RenameActions);

        // Act
        IExtendedTableSource source = TableViewFactory.CreateFocusedSource(drillDown);

        // Assert
        source.ColumnNames.Should().Contain("renamed (text)").And.NotContain("col1 (text)");
        source.TotalRows.Should().Be(1);
    }

    [Fact]
    public void CreateFocusedTableView_WithSource_ShowsSourceColumnsAndWiresMorphCallback()
    {
        // Arrange
        using Fixture fixture = new();
        IExtendedTableSource source = TableViewFactory.CreateFocusedSource(CreateDrillDown([]));
        TableStyle style = new() { AlwaysShowHeaders = true };
        IMorphAction action = RenameActions[0];

        // Act
        using FocusedTableView view = fixture.Factory.CreateFocusedTableView(source, style);
        Action<IMorphAction>? handler = view.OnMorphAction;
        handler.Should().NotBeNull();
        handler.Invoke(action);

        // Assert
        view.Table.Should().NotBeNull().And.Subject.As<ITableSource>().ColumnNames.Should().Equal(source.ColumnNames);
        view.Style.Should().BeSameAs(style);
        fixture.MorphActions.Should().ContainSingle().Which.Should().BeSameAs(action);
    }

    private DataRowIndexer BuildCsvIndexer()
    {
        DataRowIndexer indexer = new(_tempFiles.Create(".csv", "col1,col2\nvalue1,value2\n"));
        indexer.BuildIndex();
        return indexer;
    }

    private string JsonLinesFile() =>
        _tempFiles.Create(".jsonl", "{\"col1\": \"a\", \"col2\": \"b\"}\n{\"col1\": \"x\", \"col2\": \"y\"}\n");

    private static TableSchema CsvSchema() => CreateSchema(DataFormat.Csv);

    private static TableSchema JsonLinesSchema() => CreateSchema(DataFormat.JsonLines);

    private static TableSchema CreateSchema(DataFormat format, params string[] columnNames) => new()
    {
        SourceFormat = format,
        Columns =
        [
            .. (columnNames.Length == 0 ? ["col1", "col2"] : columnNames)
                .Select((name, index) => new ColumnSchema { Name = name, Type = ColumnType.Text, ColumnIndex = index }),
        ],
    };

    private static DrillDownState CreateDrillDown(IReadOnlyList<IMorphAction> actions) => new(
        [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
        CreateSchema(DataFormat.JsonLines),
        ViewMode.JsonLinesTree,
        KeyPath: [],
        ActionStack: actions);

    private sealed class Fixture : IDisposable
    {
        private readonly IApplication _app = TestAppFactory.Create();
        private readonly LazyTransformerRebuilder _rebuilder;

        public Fixture()
        {
            _rebuilder = new LazyTransformerRebuilder(State, () => CurrentView);
            Factory = new TableViewFactory(
                State,
                Queued.Add,
                () => CurrentView,
                _rebuilder,
                MorphActions.Add,
                static (_, _, _) => { });
        }

        public AppState State { get; } = new();
        public TableViewFactory Factory { get; }
        public List<Action> Queued { get; } = [];
        public List<IMorphAction> MorphActions { get; } = [];
        public View? CurrentView { get; set; }

        public void RunQueued()
        {
            foreach (Action callback in Queued.ToArray())
            {
                callback();
            }
        }

        public void Dispose()
        {
            State.Dispose();
            _app.Dispose();
        }
    }

    private sealed class CheckpointRaisingRowIndexer(string filePath) : IRowIndexer
    {
        private long _totalRows;

        public string FilePath => filePath;
        public long FileSize => 1000;
        public long BytesRead => 0;
        public long TotalRows => _totalRows;
        public bool IsIndexingCompleted => false;

#pragma warning disable CS0067
        public event Action? FirstCheckpointReached;
        public event Action<long, long>? ProgressChanged;
        public event Action? BuildIndexCompleted;
#pragma warning restore CS0067

        public void SetTotalRows(long totalRows) => _totalRows = totalRows;

        public void RaiseFirstCheckpointWithRows(long totalRows)
        {
            _totalRows = totalRows;
            FirstCheckpointReached?.Invoke();
        }

        public void BuildIndex(CancellationToken cancellationToken = default) { }

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => (0, 0);
    }
}

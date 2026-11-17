using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui.UI;
using Refedle.TestHelpers.App.Tui.Workers.Schema;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.IntegrationTests.App.Tui.UI;

// Lazy filter scans of the CSV / JSON Lines table views: the progress overlay lifecycle on
// the content container and its survival across the JSON Lines schema refinement rebuild.
public sealed partial class ViewManagerTests
{
    [Fact]
    public void FilterScanManager_Constructor_WithNullOverlayHost_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup required)

        // Act
        Func<FilterScanManager> act = () => new FilterScanManager(
            new FilterIndexingRunner(),
            static _ => { },
            static () => CancellationToken.None,
            static (_, _) => throw new InvalidOperationException("Not expected."),
            static _ => { },
            static () => { },
            static _ => { },
            null!,
            static () => null);

        // Assert
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("overlayHost");
    }

    private static IEnumerable<View> SelfAndDescendants(View view)
    {
        Stack<View> pending = new();
        pending.Push(view);

        while (pending.Count > 0)
        {
            View current = pending.Pop();
            yield return current;

            foreach (View child in current.SubViews)
            {
                pending.Push(child);
            }
        }
    }

    // The overlay has no progress bar; its percentage label is the one view that is unique to it
    private static bool IsProgressOverlayLabel(View view) =>
        view is Label label && label.Text.StartsWith(" Filtering", StringComparison.Ordinal);

    private static Label? FindProgressOverlayLabel(View? view) =>
        view is null ? null : SelfAndDescendants(view).OfType<Label>().FirstOrDefault(IsProgressOverlayLabel);

    [Fact]
    public async Task SwitchToCsvTable_WithFilterAction_ShowsOverlayOnContentContainerUntilScanCompletes()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        ControllableFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\nb\n"));
        using ItemCountHarness harness = new(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));

        // Act
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        await harness.Queue.RunNextAsync(); // indexing already finished, so the scan begins
        CsvTableView tableView = harness.ViewManager.GetCurrentView().Should().BeOfType<CsvTableView>().Subject;
        View labelWhileScanning = SelfAndDescendants(harness.Window).Where(IsProgressOverlayLabel).Should().ContainSingle().Subject;

        // Assert — while the scan runs, the overlay rides the content container (the table
        // view's own parent) and never enters the table view's subtree
        labelWhileScanning.SuperView.Should().BeSameAs(tableView.SuperView);
        SelfAndDescendants(tableView).Where(IsProgressOverlayLabel).Should().BeEmpty();

        // Act — complete the scan
        runner.Complete(0);
        await harness.Queue.RunNextAsync();

        // Assert — the overlay retires with the scan and the item count appears
        FindProgressOverlayLabel(harness.Window).Should().BeNull();
        GetItemCountText(harness.Window).Should().NotBeNull();
    }

    [Fact]
    public async Task SwitchToJsonLinesTableView_WithFilterAction_ShowsProgressOverlayUntilScanCompletes()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        ControllableFilterScanRunner runner = new();
        string filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        MockRowIndexer indexer = new(filePath, 1);
        using ItemCountHarness harness = new(filePath, DataFormat.JsonLines, indexer, runner, CreateColumnFilter("a"));

        // Act
        harness.ViewManager.SwitchToJsonLinesTableView(indexer, CreateItemCountSchema(DataFormat.JsonLines));
        await harness.Queue.RunNextAsync(); // indexing already finished, so the scan begins
        Label? labelWhileScanning = FindProgressOverlayLabel(harness.Window);
        runner.Complete(0);
        await harness.Queue.RunNextAsync();

        // Assert
        labelWhileScanning.Should().NotBeNull();
        FindProgressOverlayLabel(harness.Window).Should().BeNull();
        GetItemCountText(harness.Window).Should().NotBeNull();
    }

    [Fact]
    public async Task SchemaRefined_WhileLazyFilterScanRuns_KeepsProgressOverlayAndCompletesOnRebuiltView()
    {
        // Arrange — a real two-row scan whose queued tick and completion run after the rebuild
        using IApplication app = TestAppFactory.Create();
        using LazyScanHarness harness = new(
            FilterAction.Create("col1", FilterOperator.Equals, ComparisonType.Text, "a").Value);
        harness.ViewManager.SwitchToJsonLinesTableView(
            new MockRowIndexer(harness.FilePath, 2),
            CreateJsonLinesSchema("col1"));
        JsonLinesTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<JsonLinesTableView>().Subject;
        ITableSource? oldTable = view.Table;
        await harness.Queue.RunNextAsync(); // indexing already finished, so the scan begins

        // Act — rebuild the transformer on the same view while the scan's callbacks are queued
        harness.Refine("col1", "col2");
        FindProgressOverlayLabel(harness.Window).Should().NotBeNull("the overlay must survive the rebuild");
        Action finalTick = await harness.Queue.NextAsync();
        finalTick();
        View labelAfterTick = SelfAndDescendants(harness.Window).Where(IsProgressOverlayLabel).Should().ContainSingle().Subject;
        Action completion = await harness.Queue.NextAsync();
        completion();
        await harness.Runner.TaskAt(0);

        // Assert — the reused indexer keeps feeding the overlay on the rebuilt view, and the
        // completion retires it without starting a second scan
        view.Table.Should().NotBeSameAs(oldTable);
        labelAfterTick.Text.Should().Be(" Filtering… 100%");
        FindProgressOverlayLabel(harness.Window).Should().BeNull();
        harness.Runner.Tasks().Should().HaveCount(1);
        view.Table[0, 1].Should().Be("b");
    }

    [Fact]
    public async Task SwitchToCsvTable_WhenViewIsReplacedDuringScan_RetiresOverlayWithoutTouchingOldView()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        ControllableFilterScanRunner runner = new();
        DataRowIndexer indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        using ItemCountHarness harness = new(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        await harness.Queue.RunNextAsync(); // indexing already finished, so the scan begins

        // Act — replace the content view while the scan still runs
        harness.ViewManager.SwitchToFileSelection();

        // Assert — the swap already retired the overlay from the surviving host; the host
        // container is not disposed by the swap, so only Detach can have removed it here
        FindProgressOverlayLabel(harness.Window).Should().BeNull(
            "the view swap must retire the overlay while the scan is still running");

        // Act — let the orphaned scan complete
        runner.Complete(0);
        Func<Task> runStaleCompletion = () => harness.Queue.RunNextAsync();

        // Assert — the stale completion is discarded without touching the disposed table view
        await runStaleCompletion.Should().NotThrowAsync();
        harness.ViewManager.GetCurrentView().Should().BeOfType<FileSelectionView>();
        FindProgressOverlayLabel(harness.Window).Should().BeNull();
    }

    [Fact]
    public async Task SwitchToCsvTable_WhenIndexingCompletes_IncludesRowsIndexedAfterSwitch()
    {
        // Arrange — the file holds two matching rows but only the first is indexed when the
        // switch happens; the indexer reports the second before indexing finishes, so an
        // early-starting scan would snapshot only the first row
        using IApplication app = TestAppFactory.Create();
        // Not appended mid-test: the CSV table keeps the file open (FileShare.Read) while shown,
        // so a write fails on Windows with an IOException (sharing violation). JSON Lines closes
        // the file after mapping, so its variant can append.
        string filePath = CreateTempFile(".csv", "col1\na\na\n");
        GrowableRowIndexer indexer = new(filePath, "col1\n".Length);
        indexer.AddRows(1);
        RecordingFilterScanRunner runner = new();
        using ItemCountHarness harness = new(
            filePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));

        // Act — switch to the filtered table while indexing is still running
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));

        // Assert — no scan start is queued while indexing runs (the wait is bound to this
        // indexer), and neither the overlay nor the item count appears yet. The empty-queue
        // read is a bounded smoke check; the row-count assertions below verify the outcome.
        await harness.Queue.AssertNothingQueuedAsync(NothingQueuedGrace);
        FindProgressOverlayLabel(harness.Window).Should().BeNull();
        GetItemCountText(harness.Window).Should().BeNull();
        runner.Tasks().Should().BeEmpty();

        // Act — the indexer reports the second row, then indexing completes and the queued
        // scan start runs
        indexer.AddRows(1);
        indexer.Complete();
        await harness.Queue.RunNextAsync();

        // Assert — the scan covers the later-indexed row: its count includes both rows
        runner.Tasks().Should().ContainSingle();
        await harness.Queue.RunUntilAsync(() => GetItemCountText(harness.Window) is not null);
        GetItemCountText(harness.Window).Should().Be("2 items");
        FindProgressOverlayLabel(harness.Window).Should().BeNull();
    }

    [Fact]
    public async Task SwitchToJsonLinesTableView_WhenIndexingCompletes_IncludesRowsIndexedAfterSwitch()
    {
        // Arrange — same deferred-start scenario on the JSON Lines table view
        using IApplication app = TestAppFactory.Create();
        string filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        GrowableRowIndexer indexer = new(filePath, firstRowByteOffset: 0);
        indexer.AddRows(1);
        RecordingFilterScanRunner runner = new();
        using ItemCountHarness harness = new(
            filePath, DataFormat.JsonLines, indexer, runner, CreateColumnFilter("a"));

        // Act — switch to the filtered table while indexing is still running
        harness.ViewManager.SwitchToJsonLinesTableView(indexer, CreateItemCountSchema(DataFormat.JsonLines));

        // Assert — no scan start is queued while indexing runs, and neither the overlay nor
        // the item count appears yet. The empty-queue read is a bounded smoke check; the
        // row-count assertions below verify the outcome.
        await harness.Queue.AssertNothingQueuedAsync(NothingQueuedGrace);
        FindProgressOverlayLabel(harness.Window).Should().BeNull();
        GetItemCountText(harness.Window).Should().BeNull();
        runner.Tasks().Should().BeEmpty();

        // Act — the late row lands, then indexing completes and the queued scan start runs
        File.AppendAllText(filePath, "{\"col1\": \"a\"}\n");
        indexer.AddRows(1);
        indexer.Complete();
        await harness.Queue.RunNextAsync();

        // Assert — the scan covers the late row: its count includes both rows
        runner.Tasks().Should().ContainSingle();
        await harness.Queue.RunUntilAsync(() => GetItemCountText(harness.Window) is not null);
        GetItemCountText(harness.Window).Should().Be("2 items");
    }

    [Fact]
    public async Task SwitchToCsvTable_WhenIndexingCompletesEarly_ScansRowsIndexedSoFarWithoutErrorView()
    {
        // Arrange — the file holds two matching rows, but the completion event fires with
        // only the first row indexed, modeling an index that ended before the whole file
        using IApplication app = TestAppFactory.Create();
        string filePath = CreateTempFile(".csv", "col1\na\na\n");
        GrowableRowIndexer indexer = new(filePath, "col1\n".Length);
        indexer.AddRows(1);
        RecordingFilterScanRunner runner = new();
        using ItemCountHarness harness = new(
            filePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));

        // Act — the index completes having covered only the first row
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        indexer.Complete();
        await harness.Queue.RunNextAsync();

        // Assert — the scan starts with the rows known at completion; no error view appears
        runner.Tasks().Should().ContainSingle();
        await harness.Queue.RunUntilAsync(() => GetItemCountText(harness.Window) is not null);
        GetItemCountText(harness.Window).Should().Be("1 items");
        harness.State.CurrentMode.Should().Be(ViewMode.CsvTable);
        harness.ViewManager.GetCurrentView().Should().BeOfType<CsvTableView>();
    }

    /// <summary>
    /// Row indexer double whose indexing the test finishes manually: rows can be added while
    /// indexing "runs", and completing raises BuildIndexCompleted so the completion waits wired
    /// through the ViewManager fire on this indexer. Every checkpoint resolves to
    /// <paramref name="firstRowByteOffset"/>, the first data row's position — after the CSV
    /// header line, or zero for headerless formats.
    /// </summary>
    private sealed class GrowableRowIndexer(string filePath, long firstRowByteOffset) : IRowIndexer
    {
#pragma warning disable CS0067
        public event Action? FirstCheckpointReached;
        public event Action<long, long>? ProgressChanged;
#pragma warning restore CS0067
        public event Action? BuildIndexCompleted;

        private long _totalRows;

        public string FilePath => filePath;
        public long FileSize => 1000;
        public long BytesRead => 1000;
        public long TotalRows => _totalRows;
        public bool IsIndexingCompleted { get; private set; }

        public void AddRows(long rows) => _totalRows += rows;

        public void Complete()
        {
            IsIndexingCompleted = true;
            BuildIndexCompleted?.Invoke();
        }

        public void BuildIndex(CancellationToken ct = default)
        {
        }

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => (firstRowByteOffset, 0);
    }

    /// <summary>
    /// JSON Lines table view wired to a real <see cref="FilterIndexingRunner"/> behind a captured
    /// UI-invoke queue, so the lazy scan's tick and completion run only when the test executes them.
    /// </summary>
    private sealed class LazyScanHarness : IDisposable
    {
        private readonly Terminal.Gui.App.IApplication _app = TestAppFactory.Create();
        private readonly Window _window = new();
        private readonly StatusBar _statusBar = new();

        public LazyScanHarness(FilterAction filter)
        {
            FilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jsonl");
            File.WriteAllText(FilePath, "{\"col1\": \"a\", \"col2\": \"b\"}\n{\"col1\": \"a\", \"col2\": \"y\"}\n");
            State.StartNewFile(FilePath);
            State.AddMorphAction(filter);
            _window.Add(_statusBar);
            ModeController modeController = new(State, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager = new ViewManager(_window, State, modeController, Runner, Queue.Enqueue);
        }

        public string FilePath { get; }

        public AppState State { get; } = new();

        public Window Window => _window;

        public ViewManager ViewManager { get; }

        public RecordingFilterScanRunner Runner { get; } = new();

        public CallbackQueue Queue { get; } = new();

        public void Refine(params string[] columns)
        {
            State.OnSchemaRefined.Should().NotBeNull();
            State.OnSchemaRefined(CreateJsonLinesSchema(columns));
        }

        public void Dispose()
        {
            ViewManager.Dispose();
            _window.Dispose();
            _statusBar.Dispose();
            State.Dispose();
            _app.Dispose();
            File.Delete(FilePath);
        }
    }
}

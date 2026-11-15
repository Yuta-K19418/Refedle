using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.Tests.App.Tui.Workers.Schema;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

// Lazy filter scans of the CSV / JSON Lines table views: the progress overlay lifecycle on
// the live content view and its survival across the JSON Lines schema refinement rebuild.
public sealed partial class ViewManagerTests
{
    private static ProgressBar? FindProgressOverlayBar(View? view) =>
        view?.SubViews.OfType<ProgressBar>().FirstOrDefault();

    [Fact]
    public async Task SwitchToCsvTable_WithFilterAction_ShowsProgressOverlayUntilScanCompletes()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\nb\n"));
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));

        // Act
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        var barWhileScanning = FindProgressOverlayBar(harness.ViewManager.GetCurrentView());
        runner.Complete(0);
        await harness.Queue.RunNextAsync();

        // Assert — the overlay rides on the table view while the scan runs, then retires
        barWhileScanning.Should().NotBeNull();
        FindProgressOverlayBar(harness.ViewManager.GetCurrentView()).Should().BeNull();
        GetItemCountText(harness.Window).Should().NotBeNull();
    }

    [Fact]
    public async Task SwitchToJsonLinesTableView_WithFilterAction_ShowsProgressOverlayUntilScanCompletes()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        var indexer = new MockRowIndexer(filePath, 1);
        using var harness = new ItemCountHarness(filePath, DataFormat.JsonLines, indexer, runner, CreateColumnFilter("a"));

        // Act
        harness.ViewManager.SwitchToJsonLinesTableView(indexer, CreateItemCountSchema(DataFormat.JsonLines));
        var barWhileScanning = FindProgressOverlayBar(harness.ViewManager.GetCurrentView());
        runner.Complete(0);
        await harness.Queue.RunNextAsync();

        // Assert
        barWhileScanning.Should().NotBeNull();
        FindProgressOverlayBar(harness.ViewManager.GetCurrentView()).Should().BeNull();
        GetItemCountText(harness.Window).Should().NotBeNull();
    }

    [Fact]
    public async Task SchemaRefined_WhileLazyFilterScanRuns_KeepsProgressOverlayAndCompletesOnRebuiltView()
    {
        // Arrange — a real two-row scan whose queued tick and completion run after the rebuild
        using var app = TestAppFactory.Create();
        using var harness = new LazyScanHarness(
            FilterAction.Create("col1", FilterOperator.Equals, ComparisonType.Text, "a").Value);
        harness.ViewManager.SwitchToJsonLinesTableView(
            new MockRowIndexer(harness.FilePath, 2),
            CreateJsonLinesSchema("col1"));
        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<JsonLinesTableView>().Subject;
        var oldTable = view.Table;

        // Act — rebuild the transformer on the same view while the scan's callbacks are queued
        harness.Refine("col1", "col2");
        view.SubViews.OfType<ProgressBar>().Should().ContainSingle("the overlay must survive the rebuild");
        var finalTick = await harness.Queue.NextAsync();
        finalTick();
        var barAfterTick = view.SubViews.OfType<ProgressBar>().Should().ContainSingle().Subject;
        var completion = await harness.Queue.NextAsync();
        completion();
        await harness.Runner.TaskAt(0);

        // Assert — the reused indexer keeps feeding the overlay on the rebuilt view, and the
        // completion retires it without starting a second scan
        view.Table.Should().NotBeSameAs(oldTable);
        barAfterTick.Fraction.Should().Be(1f);
        FindProgressOverlayBar(view).Should().BeNull();
        harness.Runner.Tasks().Should().HaveCount(1);
        view.Table[0, 1].Should().Be("b");
    }

    [Fact]
    public async Task SwitchToCsvTable_WhenViewIsReplacedDuringScan_RetiresOverlayWithoutTouchingOldView()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));

        // Act — replace the content view, then let the orphaned scan complete
        harness.ViewManager.SwitchToFileSelection();
        runner.Complete(0);
        var runStaleCompletion = () => harness.Queue.RunNextAsync();

        // Assert — the stale completion is discarded without touching the disposed table view
        await runStaleCompletion.Should().NotThrowAsync();
        harness.ViewManager.GetCurrentView().Should().BeOfType<FileSelectionView>();
        FindProgressOverlayBar(harness.ViewManager.GetCurrentView()).Should().BeNull();
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
            var modeController = new ModeController(State, action => action(), TestSchemaScannerFactories.JsonLines);
            ViewManager = new ViewManager(_window, State, modeController, Runner, Queue.Enqueue);
        }

        public string FilePath { get; }

        public AppState State { get; } = new();

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

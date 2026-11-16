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
// the content container and its survival across the JSON Lines schema refinement rebuild.
public sealed partial class ViewManagerTests
{
    [Fact]
    public void FilterScanManager_Constructor_WithNullOverlayHost_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup required)

        // Act
        var act = () => new FilterScanManager(
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
        var pending = new Stack<View>();
        pending.Push(view);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            yield return current;

            foreach (var child in current.SubViews)
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
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\nb\n"));
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));

        // Act
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        var tableView = harness.ViewManager.GetCurrentView().Should().BeOfType<CsvTableView>().Subject;
        var labelWhileScanning = SelfAndDescendants(harness.Window).Where(IsProgressOverlayLabel).Should().ContainSingle().Subject;

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
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n");
        var indexer = new MockRowIndexer(filePath, 1);
        using var harness = new ItemCountHarness(filePath, DataFormat.JsonLines, indexer, runner, CreateColumnFilter("a"));

        // Act
        harness.ViewManager.SwitchToJsonLinesTableView(indexer, CreateItemCountSchema(DataFormat.JsonLines));
        var labelWhileScanning = FindProgressOverlayLabel(harness.Window);
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
        FindProgressOverlayLabel(harness.Window).Should().NotBeNull("the overlay must survive the rebuild");
        var finalTick = await harness.Queue.NextAsync();
        finalTick();
        var labelAfterTick = SelfAndDescendants(harness.Window).Where(IsProgressOverlayLabel).Should().ContainSingle().Subject;
        var completion = await harness.Queue.NextAsync();
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
        using var app = TestAppFactory.Create();
        var runner = new ControllableFilterScanRunner();
        var indexer = BuildCsvIndexer(CreateTempFile(".csv", "col1\na\n"));
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.Csv, indexer, runner, CreateColumnFilter("a"));
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));

        // Act — replace the content view while the scan still runs
        harness.ViewManager.SwitchToFileSelection();

        // Assert — the swap already retired the overlay from the surviving host; the host
        // container is not disposed by the swap, so only Detach can have removed it here
        FindProgressOverlayLabel(harness.Window).Should().BeNull(
            "the view swap must retire the overlay while the scan is still running");

        // Act — let the orphaned scan complete
        runner.Complete(0);
        var runStaleCompletion = () => harness.Queue.RunNextAsync();

        // Assert — the stale completion is discarded without touching the disposed table view
        await runStaleCompletion.Should().NotThrowAsync();
        harness.ViewManager.GetCurrentView().Should().BeOfType<FileSelectionView>();
        FindProgressOverlayLabel(harness.Window).Should().BeNull();
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

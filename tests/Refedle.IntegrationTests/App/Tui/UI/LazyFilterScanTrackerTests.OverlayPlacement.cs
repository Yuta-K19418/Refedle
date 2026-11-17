using System.Threading.Channels;
using AwesomeAssertions;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine.Filtering;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.IntegrationTests.App.Tui.UI;

// LazyFilterScanTracker in isolation: the deferred start behind the row-indexing wait, the
// overlay lifecycle on the CSV / JSON Lines table views, progress handling, and the
// stale-notification discard for every retirement path.
public sealed class LazyFilterScanTrackerTests
{
    /// <summary>
    /// Indexer double whose progress the test raises manually, so tick handling is observed
    /// without a running background build.
    /// </summary>
    private sealed class SteppingFilterRowIndexer : IFilterRowIndexer
    {
        public event Action<int, int>? ProgressChanged;

        public int TotalMatchedRows => 0;

        public int GetSourceRow(int filteredRow) => -1;

        public Task BuildIndexAsync(CancellationToken ct) => Task.CompletedTask;

        public void RaiseProgress(int scannedRows, int totalRows) =>
            ProgressChanged?.Invoke(scannedRows, totalRows);
    }

    /// <summary>
    /// <see cref="IFilterScanRunner"/> double whose scans stay pending until the test ends
    /// them, so completion handling is observed deterministically.
    /// </summary>
    private sealed class ControllableFilterScanRunner : IFilterScanRunner
    {
        private readonly List<TaskCompletionSource> _scans = [];
        private readonly List<CancellationToken> _tokens = [];

        public List<CancellationToken> Tokens => _tokens;

        public Task RunAsync(IFilterRowIndexer filterIndexer, CancellationToken ct)
        {
            _tokens.Add(ct);
            TaskCompletionSource scan = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _scans.Add(scan);
            return scan.Task;
        }

        public void Complete(int callIndex) => _scans[callIndex].TrySetResult();

        public void Cancel(int callIndex) => _scans[callIndex].TrySetCanceled();

        public void End(int callIndex) =>
            _scans[callIndex].TrySetException(new InvalidOperationException("scan failed"));
    }

    /// <summary>
    /// UI-invoke capture read through a channel with a bounded wait, so a worker continuation
    /// that has not queued its callback yet fails the test instead of racing it.
    /// </summary>
    private sealed class AwaitableCallbackQueue
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private readonly Channel<Action> _channel = Channel.CreateUnbounded<Action>();

        public void Enqueue(Action callback) => _channel.Writer.TryWrite(callback);

        public async Task<Action> NextAsync()
        {
            using CancellationTokenSource cts = new(Timeout);
            return await _channel.Reader.ReadAsync(cts.Token);
        }

        public async Task RunPendingAsync(int expectedCount)
        {
            for (int i = 0; i < expectedCount; i++)
            {
                (await NextAsync())();
            }

            _channel.Reader.TryPeek(out _).Should().BeFalse("no unexpected callback must remain queued");
        }
    }

    private sealed class TestTableView : MorphTableView;

    private sealed class TrackerHarness : IDisposable
    {
        private readonly Terminal.Gui.App.IApplication _app;
        private readonly bool _ownsAppAndTop;
        private readonly CancellationTokenSource _sessionCts = new();
        private readonly Window _top = new() { Width = Dim.Fill(), Height = Dim.Fill() };

        // An externally supplied application (e.g. owned by a live-pump session) is left to its
        // owner: the harness then disposes neither the application nor the top window, which the
        // session runs and disposes itself.
        public TrackerHarness(long totalRows = 5, Terminal.Gui.App.IApplication? externalApp = null)
        {
            _ownsAppAndTop = externalApp is null;
            _app = externalApp ?? TestAppFactory.Create();
            // Mirrors the production tree: Window → content container (overlay host) → table view
            OverlayHost = new View
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };
            View = new TestTableView
            {
                Table = new PagedTableSource(new NumberedTableSource(totalRows)),
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };
            OverlayHost.Add(View);
            _top.Add(OverlayHost);
            CurrentView = View;
            Tracker = new LazyFilterScanTracker(
                Runner,
                Pending.Enqueue,
                () => _sessionCts.Token,
                () => RefreshCount++,
                Errors.Add,
                OverlayHost,
                () => CurrentView);
        }

        public AwaitableCallbackQueue Pending { get; } = new();

        public ControllableFilterScanRunner Runner { get; } = new();

        public LazyFilterScanTracker Tracker { get; }

        public View OverlayHost { get; }

        public Window Top => _top;

        public Terminal.Gui.App.IApplication App => _app;

        public TestTableView View { get; }

        public View CurrentView { get; set; }

        public int RefreshCount { get; private set; }

        public List<string> Errors { get; } = [];

        public SteppingFilterRowIndexer Indexer { get; } = new();

        /// <summary>Completes when the test lets the row indexing finish.</summary>
        public TaskCompletionSource Indexing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Starts a scan that waits for <see cref="Indexing"/> before it begins.</summary>
        public Task StartScanAsync() => Tracker.StartScanAsync(Indexer, View, Indexing.Task);

        public void CompleteIndexing() => Indexing.TrySetResult();

        /// <summary>Simulates the next file session starting, which retires the old token.</summary>
        public void StartNextSession() => _sessionCts.Cancel();

        public void Dispose()
        {
            Tracker.Dispose();
            _sessionCts.Dispose();

            if (_ownsAppAndTop)
            {
                _top.Dispose();
                _app.Dispose();
            }
        }
    }

    private sealed class NumberedTableSource(long totalRows) : IExtendedTableSource
    {
        public long TotalRows => totalRows;
        public int Columns => 1;
        public string[] ColumnNames => ["a"];
        public string[] RawColumnNames => ["a"];
        public object this[long row, int col] => $"row{row}";
    }

    private static Label GetOverlayLabel(TrackerHarness harness) =>
        harness.OverlayHost.SubViews.OfType<Label>().Should().ContainSingle().Subject;

    [Fact]
    public async Task StartScan_PlacesOverlayAtHostBottomRegardlessOfTableRowCount()
    {
        // Arrange — the table content (50k rows) is far taller than the host viewport; the
        // session owns the application and its loop, so layout runs on the loop thread
        await using LivePumpTestSession<TrackerHarness> session = await LivePumpTestSession.StartAsync(app =>
        {
            TrackerHarness harness = new(totalRows: 50_000, externalApp: app);
            return (harness.Top, harness);
        });

        // Act — UI work runs in synchronous delegates on the loop thread; only the thread-safe
        // CompleteIndexing and the bounded queue read happen on the test thread
        TrackerHarness harness = await session.InvokeAsync((app, h) =>
        {
            app.LayoutAndDraw();
            _ = h.StartScanAsync();
            return Task.FromResult(h);
        });
        harness.CompleteIndexing();
        Action beginScan = await harness.Pending.NextAsync();
        await harness.Pending.RunPendingAsync(expectedCount: 0); // the scan start is the only queued callback
        (int hostContentHeight, int tableContentHeight, int overlayY) = await session.InvokeAsync((app, h) =>
        {
            beginScan();
            app.LayoutAndDraw();

            return Task.FromResult((
                h.OverlayHost.GetContentSize().Height,
                h.View.GetContentSize().Height,
                GetOverlayLabel(h).Frame.Y));
        });

        // Assert — the strip stays on the host's bottom row even though the table content
        // extends far below the viewport
        tableContentHeight.Should().BeGreaterThan(hostContentHeight);
        overlayY.Should().Be(hostContentHeight - 1);
    }
}

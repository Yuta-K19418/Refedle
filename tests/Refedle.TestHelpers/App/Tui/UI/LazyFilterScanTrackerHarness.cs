using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.TestHelpers.App.Tui.UI;

internal sealed class LazyFilterScanTrackerHarness : IDisposable
{
    private readonly Terminal.Gui.App.IApplication _app;
    private readonly bool _ownsAppAndTop;
    private readonly CancellationTokenSource _sessionCts = new();
    private readonly Window _top = new() { Width = Dim.Fill(), Height = Dim.Fill() };

    // An externally supplied application (e.g. owned by a live-pump session) is left to its
    // owner: the harness then disposes neither the application nor the top window, which the
    // session runs and disposes itself.
    public LazyFilterScanTrackerHarness(long totalRows = 5, Terminal.Gui.App.IApplication? externalApp = null)
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
        View = new PlainMorphTableView
        {
            Table = new PagedTableSource(new SingleColumnNumberedTableSource(totalRows)),
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

    public PendingFilterScanRunner Runner { get; } = new();

    public LazyFilterScanTracker Tracker { get; }

    public View OverlayHost { get; }

    public Window Top => _top;

    public Terminal.Gui.App.IApplication App => _app;

    public PlainMorphTableView View { get; }

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

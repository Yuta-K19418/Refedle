using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine.Filtering;
using Terminal.Gui.ViewBase;

namespace Refedle.App.Tui.UI;

/// <summary>
/// Owns the lazy filter scan attached to a CSV / JSON Lines table view: the progress overlay
/// on the stable overlay host, the per-tick row refreshes while the scan runs, the item-count
/// refresh when it completes, and the error view shown when it faults. <see cref="ViewManager"/>
/// drives it through <see cref="StartScan"/>, <see cref="Detach"/> and <see cref="Dispose"/>;
/// which scan is current stays private to this class. Unlike the FocusedTable scan, the
/// background build observes only the file-session token — replacing the content view retires
/// the presentation (overlay and subscription) but does not cancel the build itself.
/// </summary>
/// <remarks>
/// Threading contract: all tracker state is UI-thread exclusive. Worker progress and
/// completion notifications re-enter through the injected UI-thread dispatcher before they
/// touch any state; each queued callback re-verifies that its scan is still the current one
/// and is discarded otherwise.
/// </remarks>
internal sealed class LazyFilterScanTracker(
    IFilterScanRunner filterScanRunner,
    Action<Action> uiThreadInvoke,
    Func<CancellationToken> parentToken,
    Action refreshStatusBarHints,
    Action<string> showError,
    View overlayHost,
    Func<View?> getCurrentView) : IDisposable
{
    private readonly IFilterScanRunner _filterScanRunner =
        filterScanRunner ?? throw new ArgumentNullException(nameof(filterScanRunner));
    private readonly Action<Action> _uiThreadInvoke =
        uiThreadInvoke ?? throw new ArgumentNullException(nameof(uiThreadInvoke));
    private readonly Func<CancellationToken> _parentToken =
        parentToken ?? throw new ArgumentNullException(nameof(parentToken));
    private readonly Action _refreshStatusBarHints =
        refreshStatusBarHints ?? throw new ArgumentNullException(nameof(refreshStatusBarHints));
    private readonly Action<string> _showError =
        showError ?? throw new ArgumentNullException(nameof(showError));
    private readonly View _overlayHost =
        overlayHost ?? throw new ArgumentNullException(nameof(overlayHost));
    private readonly Func<View?> _getCurrentView =
        getCurrentView ?? throw new ArgumentNullException(nameof(getCurrentView));
    private readonly FilterScanProgressOverlay _overlay = new();
    private ActiveScan? _activeScan;
    private bool _disposed;

    private sealed record ActiveScan(IFilterRowIndexer Indexer, MorphTableView ScanView, Action<int, int> ProgressHandler);

    /// <summary>
    /// Gets a value indicating whether the current content view has a running lazy scan,
    /// which keeps the item count label hidden until the scan completes.
    /// </summary>
    internal bool IsRunningForCurrentView =>
        _activeScan is { } scan && ReferenceEquals(_getCurrentView(), scan.ScanView);

    /// <summary>
    /// Starts the lazy scan for <paramref name="view"/>: subscribes the indexer's progress,
    /// shows the overlay on the overlay host and runs the background build. Replaces any scan
    /// still attached from a previous view. Runs on the UI thread, after the view became the
    /// current content view.
    /// </summary>
    internal void StartScan(IFilterRowIndexer indexer, MorphTableView view)
    {
        Detach();

        // Captured by value: loading another file cancels this token, which marks the
        // outcome stale before its completion callback runs.
        var sessionToken = _parentToken();
        bool isScanCurrent() =>
            !_disposed
            && !sessionToken.IsCancellationRequested
            && _activeScan is { Indexer: var activeIndexer, ScanView: var activeView }
            && ReferenceEquals(activeIndexer, indexer)
            && ReferenceEquals(activeView, view)
            && ReferenceEquals(_getCurrentView(), view);
        Action<int, int> progressHandler = (scannedRows, totalRows) =>
            _uiThreadInvoke(() => HandleProgress(isScanCurrent, view, scannedRows, totalRows));

        var scan = new ActiveScan(indexer, view, progressHandler);
        _activeScan = scan;
        indexer.ProgressChanged += progressHandler;
        // The host's content size is its viewport, so the anchored overlay stays in the visible
        // area; on the table view it would follow the row count and leave the viewport.
        _overlay.Show(_overlayHost);

        _ = RunScanAsync(scan, isScanCurrent, sessionToken);
    }

    /// <summary>
    /// Retires the active scan's presentation — unsubscribes progress and dismisses the
    /// overlay from its host — without touching the background build itself. Called when the
    /// content view is about to be replaced, so the overlay never lingers over the next view.
    /// </summary>
    internal void Detach()
    {
        var scan = _activeScan;
        _activeScan = null;

        if (scan is null)
        {
            return;
        }

        scan.Indexer.ProgressChanged -= scan.ProgressHandler;
        _overlay.Dismiss();
    }

    /// <summary>
    /// Runs the background build and hands the outcome to the UI thread: the item count
    /// refresh for a successful scan of the still-current view, and the error view for a
    /// faulted one.
    /// </summary>
    private async Task RunScanAsync(ActiveScan scan, Func<bool> isScanCurrent, CancellationToken sessionToken)
    {
        var scanTask = _filterScanRunner.RunAsync(scan.Indexer, sessionToken);

        // WhenAny never throws, so a canceled or faulted scan is observed without a catch-all.
        await Task.WhenAny(scanTask).ConfigureAwait(false);
        var succeeded = scanTask.IsCompletedSuccessfully;
        var failureMessage = scanTask.Exception?.GetBaseException().Message;

        _uiThreadInvoke(() => CompleteScan(scan, isScanCurrent, succeeded, failureMessage));
    }

    /// <summary>
    /// Applies a progress tick on the UI thread: advances the overlay and refreshes the
    /// table so rows found since the last tick become visible without a key press.
    /// Discards ticks of a scan that is no longer current.
    /// </summary>
    private void HandleProgress(Func<bool> isScanCurrent, MorphTableView view, int scannedRows, int totalRows)
    {
        if (!isScanCurrent())
        {
            return;
        }

        _overlay.Update(scannedRows, totalRows);
        view.Update();
        view.SetNeedsDraw();
    }

    /// <summary>
    /// Applies the scan outcome on the UI thread. Every ended scan unsubscribes, and the one
    /// still owning the overlay retires it; a canceled scan (neither succeeded nor failed) changes nothing else, as
    /// its view is being replaced. A stale outcome — the view was replaced, the file session
    /// ended, or the tracker was disposed — is discarded. Only a successful scan of the
    /// still-current view refreshes the status bar, and only a faulted one shows the error view.
    /// </summary>
    private void CompleteScan(ActiveScan scan, Func<bool> isScanCurrent, bool succeeded, string? failureMessage)
    {
        scan.Indexer.ProgressChanged -= scan.ProgressHandler;

        // The overlay is shared: only the scan that still owns it may retire it.
        if (ReferenceEquals(_activeScan, scan))
        {
            _overlay.Dismiss();
        }

        if (!isScanCurrent() || (!succeeded && failureMessage is null))
        {
            return;
        }

        _activeScan = null;

        if (failureMessage is not null)
        {
            _showError($"Filter scan failed: {failureMessage}");
            return;
        }

        scan.ScanView.Update();
        scan.ScanView.SetNeedsDraw();
        _refreshStatusBarHints();
    }

    /// <summary>
    /// Cancels the presentation of the active scan (if any) and releases the overlay.
    /// Must be called on the UI thread (see the class threading contract).
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Detach();
        _overlay.Dispose();
    }
}

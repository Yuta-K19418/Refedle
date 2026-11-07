using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI;

/// <summary>
/// Owns the FocusedTable filter-scan session: session initialization, the staged table
/// reveal at the display threshold, progress/failure/completion handling, and
/// cancellation when the content view is replaced. <see cref="ViewManager"/> drives it
/// through <see cref="StartScan"/>, <see cref="CancelIfNotStaged"/> and
/// <see cref="Dispose"/>; which state a scan cancels or resets stays private to this class.
/// </summary>
/// <remarks>
/// Threading contract: all FilterScanManager state is UI-thread exclusive. Worker
/// progress/completion notifications re-enter through the injected UI-thread dispatcher
/// before they touch session state; see <see cref="ViewManager"/> for the full contract.
/// </remarks>
internal sealed class FilterScanManager(
    IFilterScanRunner filterScanRunner,
    Action<Action> uiThreadInvoke,
    Func<CancellationToken> parentToken,
    Func<IExtendedTableSource, TableStyle, FocusedTableView> createFocusedTableView,
    Action<View> swapView,
    Action refreshStatusBarHints,
    Action<string> showError,
    Func<View?> getCurrentView) : IDisposable
{
    private readonly IFilterScanRunner _filterScanRunner =
        filterScanRunner ?? throw new ArgumentNullException(nameof(filterScanRunner));
    private readonly Action<Action> _uiThreadInvoke =
        uiThreadInvoke ?? throw new ArgumentNullException(nameof(uiThreadInvoke));
    private readonly Func<CancellationToken> _parentToken =
        parentToken ?? throw new ArgumentNullException(nameof(parentToken));
    private readonly Func<IExtendedTableSource, TableStyle, FocusedTableView> _createFocusedTableView =
        createFocusedTableView ?? throw new ArgumentNullException(nameof(createFocusedTableView));
    private readonly Action<View> _swapView = swapView ?? throw new ArgumentNullException(nameof(swapView));
    private readonly Action _refreshStatusBarHints =
        refreshStatusBarHints ?? throw new ArgumentNullException(nameof(refreshStatusBarHints));
    private readonly Action<string> _showError = showError ?? throw new ArgumentNullException(nameof(showError));
    private readonly Func<View?> _getCurrentView =
        getCurrentView ?? throw new ArgumentNullException(nameof(getCurrentView));
    private readonly FilterScanProgressOverlay _overlay = new();
    private FilterScanSession? _session;
    private FocusedTableView? _tableView;

    /// <summary>
    /// Replaces the content view with a loading view and starts the background filter scan.
    /// The indexer's threshold notification reveals the table view mid-scan; progress
    /// callbacks then grow the visible rows until the scan completes.
    /// Runs on the UI thread; worker notifications are marshaled back through the
    /// UI-thread dispatcher before they touch any session state.
    /// </summary>
    internal void StartScan(FocusedTableTransformer source, FocusedTableFilterRowIndexer indexer, TableStyle style)
    {
        var loadingView = LoadingView.Create("Filtering rows…");
        _swapView(loadingView);
        _refreshStatusBarHints();

        // Created after the view swap so the swap's cancellation of the previous scan
        // cannot touch this scan's token source.
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_parentToken());
        // Evaluated when a queued UI callback runs, not when it is created. Cancellation keeps
        // IsCancellationRequested readable after the CTS is disposed.
        bool isProgressCurrent()
        {
            if (_session is not { } current || !ReferenceEquals(current.Cts, cts) || cts.IsCancellationRequested)
            {
                return false;
            }

            return true;
        }

        Action<int, int> progressHandler = (scannedRows, totalRows) =>
            _uiThreadInvoke(() => HandleProgress(isProgressCurrent, scannedRows, totalRows));
        var session = new FilterScanSession(cts, indexer, source, style, progressHandler);
        indexer.ProgressChanged += progressHandler;
        _session = session;
        _tableView = null;
        _overlay.Show(loadingView);

        _ = RunScanAsync();
    }

    /// <summary>
    /// Cancels the running scan when the content view is about to be replaced by a view
    /// other than the scan's own staged replacement for the loading view; a no-op with no
    /// running scan or when the incoming view is that staged replacement.
    /// Runs on the UI thread before the swap removes the current view.
    /// </summary>
    internal void CancelIfNotStaged(View newView)
    {
        if (_session is not null && !ReferenceEquals(_tableView, newView))
        {
            CancelScan();
        }
    }

    /// <summary>
    /// Runs the background scan and awaits it in two stages: the first wait ends as soon as
    /// the indexer confirms enough matched rows (or the scan ends below the threshold) and
    /// queues the table reveal; the second awaits the scan itself and queues the completion.
    /// The token is read exactly once, before any cancellation can dispose the CTS.
    /// A non-cancellation fault is marshaled to the UI thread as a failure callback.
    /// </summary>
    private async Task RunScanAsync()
    {
        // Inputs are fixed before the first await, while this scan is still the current session
        if (_session is not { } session)
        {
            return;
        }

        bool isScanCurrent()
        {
            if (!ReferenceEquals(_session, session) || session.Cts.IsCancellationRequested)
            {
                return false;
            }

            return true;
        }

        var indexer = session.Indexer;
        var token = session.Cts.Token;

        try
        {
            var scanTask = _filterScanRunner.RunAsync(indexer, token);
            await Task.WhenAny(indexer.ThresholdReached, scanTask).ConfigureAwait(false);
            if (indexer.ThresholdReached.IsCompletedSuccessfully)
            {
                _uiThreadInvoke(() => RevealTableView(isScanCurrent));
            }

            await scanTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            _uiThreadInvoke(() => FailScan(isScanCurrent, exception));
            return;
        }

        _uiThreadInvoke(() => CompleteScan(isScanCurrent));
    }

    /// <summary>
    /// Applies a scan fault on the UI thread: retires the session exactly like the cancelled
    /// path (unsubscribe, CTS cancel + dispose, transformer dispose, overlay dismiss) and
    /// shows the error placeholder. Discards the fault of a session that is no longer
    /// current or was already canceled.
    /// </summary>
    private void FailScan(Func<bool> isScanCurrent, Exception exception)
    {
        if (!isScanCurrent())
        {
            return;
        }

        CancelScan();
        _showError($"Filter scan failed: {exception.Message}");
    }

    /// <summary>
    /// Applies the threshold notification on the UI thread: swaps the loading view for the
    /// table view with the overlay still attached. Discards the notification of a session
    /// that is no longer current, was already canceled, or already revealed its table.
    /// </summary>
    private void RevealTableView(Func<bool> isScanCurrent)
    {
        if (!isScanCurrent() || _tableView is not null || _session is not { } session)
        {
            return;
        }

        ShowTableView(session.Source, session.Style, attachOverlay: true);
    }

    /// <summary>
    /// Applies a progress tick on the UI thread. Discards ticks from a session that is no
    /// longer current or was already canceled: IsCancellationRequested stays readable after
    /// CTS disposal, so no token access is needed on this path.
    /// </summary>
    private void HandleProgress(Func<bool> isProgressCurrent, int scannedRows, int totalRows)
    {
        if (!isProgressCurrent())
        {
            return;
        }

        _overlay.Update(scannedRows, totalRows);

        if (_tableView is { } tableView)
        {
            // Rows found since the last tick become visible; the selection stays untouched
            tableView.Update();
            tableView.SetNeedsDraw();
        }
    }

    private void ShowTableView(IExtendedTableSource source, TableStyle style, bool attachOverlay)
    {
        // Detach the overlay while its host (the loading view) is still alive; the swap below disposes it
        _overlay.Dismiss();

        var tableView = _createFocusedTableView(source, style);
        tableView.SetSelection(0, 0, false);
        tableView.Update();
        _tableView = tableView;
        _swapView(tableView);
        tableView.SetFocus();
        _refreshStatusBarHints();

        if (attachOverlay)
        {
            _overlay.Show(tableView);
        }
    }

    /// <summary>
    /// Finalizes a scan that ran to completion on the UI thread. Discards the completion of
    /// a canceled session, and releases only the subscription and the CTS: the transformer's
    /// ownership moves to the table view built from it, so <see cref="FilterScanSession.Dispose"/>
    /// (which also disposes the transformer) must stay exclusive to the cancelled path.
    /// </summary>
    private void CompleteScan(Func<bool> isScanCurrent)
    {
        if (!isScanCurrent() || _session is not { } session)
        {
            return;
        }

        _session = null;
        session.Indexer.ProgressChanged -= session.ProgressHandler;
        session.Cts.Dispose();
        _tableView = null;

        // The table was already revealed by the threshold notification, and the progress
        // ticks performed the row refreshes: completion only retires the overlay. Fewer
        // matches than the display threshold ever produced leaves the loading view as the
        // current view, so the completion is the only remaining trigger that can reveal
        // the table.
        if (_getCurrentView() is FocusedTableView)
        {
            _overlay.Dismiss();
            return;
        }

        ShowTableView(session.Source, session.Style, attachOverlay: false);
    }

    /// <summary>
    /// Stops a running scan on the UI thread by disposing its session (unsubscribe,
    /// CTS cancel + dispose, transformer dispose) and retiring the scan-owned view state.
    /// </summary>
    private void CancelScan()
    {
        var session = _session;
        _session = null;
        _tableView = null;
        _overlay.Dismiss();
        session?.Dispose();
    }

    /// <summary>
    /// Cancels the running scan session (if any) and releases the overlay.
    /// Must be called on the UI thread (see the class threading contract).
    /// </summary>
    public void Dispose()
    {
        CancelScan();
        _overlay.Dispose();
    }
}

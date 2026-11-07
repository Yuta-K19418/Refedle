using Refedle.App.Tui.UI.Views;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI;

/// <summary>
/// State of the FocusedTable filter scan currently attached to the content view.
/// All session state is UI-thread exclusive: the session fields of
/// <see cref="FilterScanManager"/> (the session and its staged table view) and the session
/// CTS cancel/dispose are touched only on the UI thread, and worker progress/completion
/// notifications re-enter through the UI-thread dispatcher, which serializes them with
/// cancellation.
/// </summary>
internal sealed record FilterScanSession(
    CancellationTokenSource Cts,
    FocusedTableFilterRowIndexer Indexer,
    FocusedTableTransformer Source,
    TableStyle Style,
    Action<int, int> ProgressHandler) : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Tears the session down for the cancelled path: unsubscribes progress, cancels and
    /// disposes the CTS, then disposes the transformer. Idempotent.
    /// The completed path must not call this: there the transformer's ownership moves to
    /// the table view built from it, so completion releases only the subscription and CTS.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Indexer.ProgressChanged -= ProgressHandler;
        Cts.Cancel();
        Cts.Dispose();
        Source.Dispose();
    }
}

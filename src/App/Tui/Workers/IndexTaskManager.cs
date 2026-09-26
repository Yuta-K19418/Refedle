using Refedle.Engine.IO;

namespace Refedle.App.Tui.Workers;

/// <summary>
/// Manages the lifecycle of background indexing tasks.
/// Handles cancellation of previous tasks and cleanup on disposal.
/// </summary>
internal sealed class IndexTaskManager : IDisposable
{
    private readonly Lock _lock = new();
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// Starts a background indexing task for the specified indexer.
    /// Cancels any existing indexing task first.
    /// </summary>
    /// <param name="indexer">The indexer to start.</param>
    public void Start(IRowIndexer indexer)
    {
        ArgumentNullException.ThrowIfNull(indexer);

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Cancel previous task
            _cts?.Cancel();
            _cts?.Dispose();

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            // Start indexing in the background. No await needed as progress/completion
            // are handled via the indexer's events.
            _ = Task.Run(() => indexer.BuildIndex(ct), ct);
        }
    }

    /// <summary>
    /// Cancels the currently running indexing task without starting a new one.
    /// </summary>
    public void CancelCurrent()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            // Only cancel, don't await. Background threads check CancellationToken
            // periodically and exit cooperatively.
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _disposed = true;
        }
    }
}

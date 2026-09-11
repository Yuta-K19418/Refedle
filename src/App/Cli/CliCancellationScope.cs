namespace Refedle.App.Cli;

/// <summary>
/// Links a <see cref="CancellationToken"/> to Ctrl+C for the duration of a single CLI command,
/// and unsubscribes the Ctrl+C handler on disposal so repeated command invocations do not
/// accumulate handlers referencing disposed tokens.
/// </summary>
internal sealed class CliCancellationScope : IDisposable
{
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _cts;
    private readonly ICancelKeyPressSource _source;
    private readonly ConsoleCancelEventHandler _handler;
    private bool _disposed;

    private CliCancellationScope(CancellationTokenSource cts, ICancelKeyPressSource source)
    {
        _cts = cts;
        _source = source;
        _handler = HandleCancelKeyPress;
    }

    /// <summary>The linked token, cancelled when the outer token is cancelled or Ctrl+C is pressed.</summary>
    public CancellationToken Token => _cts.Token;

    /// <summary>
    /// Creates a scope whose token is cancelled by <paramref name="ct"/> or by a Ctrl+C press
    /// reported through <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The Ctrl+C source to subscribe to for the scope's lifetime.</param>
    /// <param name="ct">The outer cancellation token to link to.</param>
    public static CliCancellationScope Create(ICancelKeyPressSource source, CancellationToken ct)
    {
        var scope = new CliCancellationScope(CancellationTokenSource.CreateLinkedTokenSource(ct), source);
        source.CancelKeyPress += scope._handler;
        return scope;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Unsubscribing does not stop a handler invocation already in flight, so the gate also
        // guards against that invocation calling Cancel() on an already-disposed _cts below.
        _source.CancelKeyPress -= _handler;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _cts.Dispose();
    }

    private void HandleCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _cts.Cancel();
        }
    }
}

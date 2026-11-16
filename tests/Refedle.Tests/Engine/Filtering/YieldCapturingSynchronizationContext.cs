namespace Refedle.Tests.Engine.Filtering;

/// <summary>
/// Captures continuations posted by <c>Task.Yield</c> so a test can act while the scan is
/// suspended at the yield and then resume it deterministically, without sleeps.
/// </summary>
internal sealed class YieldCapturingSynchronizationContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _posted = [];

    public int PendingCount => _posted.Count;

    public override void Post(SendOrPostCallback d, object? state) => _posted.Enqueue((d, state));

    /// <summary>Starts <paramref name="start"/> with this context current, then restores the previous one.</summary>
    public Task Start(Func<Task> start)
    {
        var previous = Current;
        SetSynchronizationContext(this);
        try
        {
            return start();
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }

    /// <summary>Runs the oldest captured continuation.</summary>
    public void RunNext()
    {
        var (callback, state) = _posted.Dequeue();
        callback(state);
    }
}

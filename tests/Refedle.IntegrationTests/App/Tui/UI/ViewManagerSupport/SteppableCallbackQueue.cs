using System.Threading.Channels;
using AwesomeAssertions;

namespace Refedle.IntegrationTests.App.Tui.UI.ViewManagerSupport;

/// <summary>
/// Signal-based replacement for polling: each captured UI-thread callback is read from a channel
/// with a bounded wait, so a callback that never arrives fails the test instead of hanging it.
/// </summary>
internal sealed class SteppableCallbackQueue
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private readonly Channel<Action> _channel = Channel.CreateUnbounded<Action>();

    public void Enqueue(Action callback) => _channel.Writer.TryWrite(callback);

    public async Task<Action> NextAsync()
    {
        using CancellationTokenSource cts = new(Timeout);
        return await _channel.Reader.ReadAsync(cts.Token);
    }

    public async Task RunNextAsync() => (await NextAsync())();

    public async Task RunUntilAsync(Func<bool> isDone)
    {
        while (!isDone())
        {
            await RunNextAsync();
        }
    }

    /// <summary>
    /// Fails when any callback arrives within <paramref name="grace"/>; a callback that
    /// arrives later than the grace period is not detected by this check.
    /// </summary>
    public async Task AssertNothingQueuedAsync(TimeSpan grace)
    {
        using CancellationTokenSource cts = new(grace);
        Func<Task<Action>> read = async () => await _channel.Reader.ReadAsync(cts.Token);
        await read.Should().ThrowAsync<OperationCanceledException>(
            "no callback must be queued within the grace period");
    }
}

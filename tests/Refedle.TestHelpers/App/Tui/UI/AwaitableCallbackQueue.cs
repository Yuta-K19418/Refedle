using System.Threading.Channels;
using AwesomeAssertions;

namespace Refedle.TestHelpers.App.Tui.UI;

/// <summary>
/// UI-invoke capture read through a channel with a bounded wait, so a worker continuation
/// that has not queued its callback yet fails the test instead of racing it.
/// </summary>
internal sealed class AwaitableCallbackQueue
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

namespace Refedle.App.Cli;

/// <summary>
/// An <see cref="IAppLogger"/> that holds messages in memory so they can be replayed in order
/// once an animated indicator no longer owns the terminal.
/// </summary>
internal sealed class DeferredAppLogger : IAppLogger
{
    private enum Level
    {
        Info,
        Warning,
        Error,
    }

    private readonly Lock _gate = new();
    private readonly List<(Level Level, string Message)> _messages = [];

    /// <inheritdoc/>
    public ValueTask WriteInfoAsync(string message) => AddAsync(Level.Info, message);

    /// <inheritdoc/>
    public ValueTask WriteWarningAsync(string message) => AddAsync(Level.Warning, message);

    /// <inheritdoc/>
    public ValueTask WriteErrorAsync(string message) => AddAsync(Level.Error, message);

    /// <summary>
    /// Replays the held messages to <paramref name="target"/> in the order they were written and clears them.
    /// </summary>
    /// <param name="target">The logger that receives the messages.</param>
    /// <returns>A task that completes when all messages are written.</returns>
    public async ValueTask FlushToAsync(IAppLogger target)
    {
        ArgumentNullException.ThrowIfNull(target);

        (Level Level, string Message)[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _messages];
            _messages.Clear();
        }

        foreach (var (level, message) in snapshot)
        {
            var write = level switch
            {
                Level.Warning => target.WriteWarningAsync(message),
                Level.Error => target.WriteErrorAsync(message),
                _ => target.WriteInfoAsync(message),
            };
            await write.ConfigureAwait(false);
        }
    }

    private ValueTask AddAsync(Level level, string message)
    {
        lock (_gate)
        {
            _messages.Add((level, message));
        }

        return ValueTask.CompletedTask;
    }
}

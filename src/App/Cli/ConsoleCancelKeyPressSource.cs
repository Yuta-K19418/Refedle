namespace Refedle.App.Cli;

/// <summary>
/// The production <see cref="ICancelKeyPressSource"/>: forwards add/remove to the static
/// <see cref="Console.CancelKeyPress"/> event. Holds no state, so a single instance can be
/// shared by every scope.
/// </summary>
internal sealed class ConsoleCancelKeyPressSource : ICancelKeyPressSource
{
    /// <inheritdoc/>
    public event ConsoleCancelEventHandler? CancelKeyPress
    {
        add => Console.CancelKeyPress += value;
        remove => Console.CancelKeyPress -= value;
    }
}

namespace Refedle.App.Cli;

/// <summary>
/// The Ctrl+C signal a <see cref="CliCancellationScope"/> subscribes to, abstracted away from
/// the static <see cref="Console.CancelKeyPress"/> so unit tests can raise it deterministically.
/// </summary>
internal interface ICancelKeyPressSource
{
    /// <summary>Raised when Ctrl+C is pressed.</summary>
    event ConsoleCancelEventHandler? CancelKeyPress;
}

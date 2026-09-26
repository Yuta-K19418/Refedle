namespace Refedle.App.Cli;

/// <summary>
/// Shows an indeterminate "work in progress" indicator with the current phase name while a
/// CLI command runs.
/// </summary>
internal interface IStatusReporter
{
    /// <summary>
    /// Runs <paramref name="work"/> while an indicator is displayed.
    /// </summary>
    /// <typeparam name="T">The result type of the work.</typeparam>
    /// <param name="initialPhase">The phase name shown when the indicator starts.</param>
    /// <param name="work">The work to run; it receives a callback that switches the displayed phase name.</param>
    /// <returns>The result of <paramref name="work"/>.</returns>
    ValueTask<T> RunAsync<T>(string initialPhase, Func<Action<string>, ValueTask<T>> work);
}

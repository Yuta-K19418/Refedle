using Spectre.Console;

namespace Refedle.App.Cli;

/// <summary>
/// An <see cref="IStatusReporter"/> that renders a Spectre.Console spinner. Spectre skips the
/// animation when the output is redirected or the terminal is not interactive.
/// </summary>
internal sealed class SpectreStatusReporter : IStatusReporter
{
    /// <inheritdoc/>
    public async ValueTask<T> RunAsync<T>(string initialPhase, Func<Action<string>, ValueTask<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        var status = AnsiConsole.Status().Spinner(Spinner.Known.Dots);
        return await status.StartAsync(Markup.Escape(initialPhase), async ctx =>
        {
            void SetPhase(string phase) => ctx.Status(Markup.Escape(phase));
            return await work(SetPhase).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}

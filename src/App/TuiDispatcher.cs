using Refedle.App.Cli;

namespace Refedle.App;

/// <summary>
/// Parses TUI startup arguments and runs the interactive Terminal.Gui application.
/// </summary>
internal static class TuiDispatcher
{
    /// <summary>
    /// Parses the given arguments as TUI startup options and runs the interactive application.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The exit code for the process.</returns>
    public static async ValueTask<ExitCode> RunAsync(string[] args, CancellationToken ct)
    {
        var parseTuiResult = TuiArgumentParser.Parse(args);
        if (parseTuiResult.IsFailure)
        {
            await Console.Error.WriteLineAsync(parseTuiResult.Error).ConfigureAwait(false);
            return ExitCode.Failure;
        }

        var tuiOptions = parseTuiResult.Value;
        var missingFileError = tuiOptions.FindMissingFileError();
        if (missingFileError is not null)
        {
            await Console.Error.WriteLineAsync(missingFileError).ConfigureAwait(false);
            return ExitCode.Failure;
        }

        var result = TuiApplication.Create();
        using var app = result.app;
        using var mainWindow = result.mainWindow;

        app.Init();
        mainWindow.SubscribeKeyHandler();
        if (tuiOptions.HasAny)
        {
            mainWindow.ScheduleStartupLoad(tuiOptions);
        }

        await app.RunAsync(mainWindow, ct, errorHandler: null).ConfigureAwait(false);
        return ExitCode.Success;
    }
}

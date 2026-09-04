using Refedle.App;
using Refedle.App.Cli;

// "--version" wins even when combined with other modes (e.g. "refedle --cli --version"),
// while the "version" subcommand form is only recognized as the first argument so that a
// positional file name never triggers the version output.
if (args.Contains("--version") || args is ["version", ..])
{
    await Console.Out.WriteLineAsync($"refedle {BuildInfo.Version}");
    return (int)ExitCode.Success;
}

if (args.Contains("--cli"))
{
    var parseResult = ArgumentParser.Parse(args);
    if (parseResult.IsFailure)
    {
        await Console.Error.WriteLineAsync(parseResult.Error);
        return (int)ExitCode.Failure;
    }

    var logger = new ConsoleAppLogger();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    return (int)await Runner.RunAsync(parseResult.Value, logger, cts.Token);
}

var parseTuiResult = TuiArgumentParser.Parse(args);
if (parseTuiResult.IsFailure)
{
    await Console.Error.WriteLineAsync(parseTuiResult.Error);
    return (int)ExitCode.Failure;
}

var tuiOptions = parseTuiResult.Value;
if (tuiOptions.InputFile is not null && !File.Exists(tuiOptions.InputFile))
{
    await Console.Error.WriteLineAsync($"Error: File not found: {tuiOptions.InputFile}");
    return (int)ExitCode.Failure;
}

if (tuiOptions.RecipeFile is not null && !File.Exists(tuiOptions.RecipeFile))
{
    await Console.Error.WriteLineAsync($"Error: Recipe file not found: {tuiOptions.RecipeFile}");
    return (int)ExitCode.Failure;
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

await app.RunAsync(mainWindow, CancellationToken.None, errorHandler: null);
return (int)ExitCode.Success;

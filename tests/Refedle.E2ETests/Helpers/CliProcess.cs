using System.Diagnostics;

namespace Refedle.E2ETests.Helpers;

/// <summary>
/// The observable outcome of a refedle CLI child process run.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">The full standard output text.</param>
/// <param name="StandardError">The full standard error text.</param>
internal sealed record CliProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Launches the real refedle binary as a child process and captures its black-box outcome.
/// </summary>
internal static class CliProcess
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Runs <c>refedle apply</c> with the given batch-conversion arguments and waits for exit.
    /// </summary>
    /// <param name="inputFile">The path passed to <c>--input</c>.</param>
    /// <param name="recipeFile">The path passed to <c>--recipe</c>.</param>
    /// <param name="outputFile">The path passed to <c>--output</c>.</param>
    /// <param name="workingDirectory">The working directory for the child process.</param>
    /// <returns>The exit code and captured output streams of the process.</returns>
    /// <exception cref="TimeoutException">Thrown when the process does not exit in time.</exception>
    public static async Task<CliProcessResult> RunAsync(string inputFile, string recipeFile, string outputFile, string workingDirectory)
    {
        var startInfo = CreateStartInfo(workingDirectory);
        startInfo.ArgumentList.Add("apply");
        startInfo.ArgumentList.Add("--input");
        startInfo.ArgumentList.Add(inputFile);
        startInfo.ArgumentList.Add("--recipe");
        startInfo.ArgumentList.Add(recipeFile);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputFile);

        return await RunProcessAsync(startInfo).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs <c>refedle</c> with raw arguments (without the <c>apply</c> subcommand) and waits for exit.
    /// </summary>
    /// <param name="arguments">The raw arguments passed to the refedle binary.</param>
    /// <returns>The exit code and captured output streams of the process.</returns>
    /// <exception cref="TimeoutException">Thrown when the process does not exit in time.</exception>
    public static async Task<CliProcessResult> RunWithArgumentsAsync(IReadOnlyList<string> arguments)
    {
        var startInfo = CreateStartInfo(workingDirectory: null);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return await RunProcessAsync(startInfo).ConfigureAwait(false);
    }

    private static ProcessStartInfo CreateStartInfo(string? workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(AppPathResolver.AppDllPath);
        return startInfo;
    }

    private static async Task<CliProcessResult> RunProcessAsync(ProcessStartInfo startInfo)
    {
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = new CancellationTokenSource(Timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Kill first so the redirected streams reach EOF and the read tasks complete.
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw new TimeoutException($"The refedle CLI process did not exit within {Timeout}.");
        }

        var standardOutput = await standardOutputTask.ConfigureAwait(false);
        var standardError = await standardErrorTask.ConfigureAwait(false);
        return new CliProcessResult(process.ExitCode, standardOutput, standardError);
    }
}

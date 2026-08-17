using System.Text;
using Refedle.App;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Helpers;

/// <summary>
/// Drives a Refedle TUI session against the ANSI driver, forced into its documented headless mode
/// (buffer-only I/O, no real console/tty calls) via <c>DisableRealDriverIO=1</c>, so tests can run
/// the real view/key-handling stack and poll rendered screen content.
/// </summary>
internal sealed class TuiTestHarness : IAsyncDisposable
{
    private const int Cols = 80;
    private const int Rows = 24;
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    private readonly IApplication _app;
    private readonly MainWindow _mainWindow;
    private readonly CancellationTokenSource _stopCts;
    private readonly Task<object?> _runTask;

    private TuiTestHarness(
        IApplication app,
        MainWindow mainWindow,
        CancellationTokenSource stopCts,
        Task<object?> runTask)
    {
        _app = app;
        _mainWindow = mainWindow;
        _stopCts = stopCts;
        _runTask = runTask;
    }

    public MainWindow MainWindow => _mainWindow;

    /// <summary>
    /// Creates the application, initializes it on the ANSI driver, fixes the screen to 80x24,
    /// subscribes key handling, and starts the real event loop — all on one dedicated background
    /// thread — so key handling, background indexing, and drawing all run for real.
    /// </summary>
    /// <remarks>
    /// Terminal.Gui records the thread that calls <c>Init</c> as its UI thread
    /// (<see cref="IApplication.MainThreadId"/>) and treats <see cref="IApplication.Invoke(Action)"/>
    /// calls made from that same thread as synchronous instead of queued. Everything from
    /// <c>Init</c> onward must therefore run on the same thread that later pumps the loop, or
    /// <c>Invoke</c> calls made from the caller could race the loop's own draw. Despite its name,
    /// <see cref="IApplication.RunAsync(Terminal.Gui.App.IRunnable, CancellationToken, Func{Exception, bool}?)"/>
    /// also runs the whole blocking UI loop synchronously on the calling thread and only returns
    /// once stopped, so this whole sequence is dispatched via <c>Task.Run</c> to let the test drive
    /// it concurrently. Stopping goes through the cancellation token rather than a direct
    /// <c>RequestStop</c> call, since that is the mechanism the SDK itself uses to marshal a stop
    /// request onto the loop's own thread.
    /// </remarks>
    public static async Task<TuiTestHarness> StartAsync()
    {
        // Forces Terminal.Gui's own documented headless mode instead of relying on its
        // auto-detection, which is not guaranteed to agree across 3-OS CI runners.
        var previousDisableRealDriverIO = Environment.GetEnvironmentVariable("DisableRealDriverIO");
        Environment.SetEnvironmentVariable("DisableRealDriverIO", "1");

        var ready = new TaskCompletionSource<(IApplication App, MainWindow MainWindow)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stopCts = new CancellationTokenSource();

        // The worker owns app/mainWindow disposal and DisableRealDriverIO restoration, and only
        // after RunAsync has actually returned — never the caller — so neither races the loop
        // thread. TuiApplication.Create() itself is inside the outer try so that even a failure
        // there still completes `ready` with the exception instead of hanging StartAsync forever.
        var runTask = Task.Run(() =>
        {
            try
            {
                var (app, mainWindow) = TuiApplication.Create();
                try
                {
                    app.Init(DriverRegistry.Names.ANSI);
                    var driver = app.Driver
                        ?? throw new InvalidOperationException("IApplication.Driver was not set after Init.");
                    driver.SetScreenSize(Cols, Rows);
                    mainWindow.SubscribeKeyHandler();
                    ready.SetResult((app, mainWindow));

                    return app.RunAsync(mainWindow, stopCts.Token, errorHandler: null);
                }
                finally
                {
                    try
                    {
                        mainWindow.Dispose();
                    }
                    finally
                    {
                        app.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                ready.TrySetException(ex);
                throw;
            }
            finally
            {
                Environment.SetEnvironmentVariable("DisableRealDriverIO", previousDisableRealDriverIO);
            }
        });

        try
        {
            var (app, mainWindow) = await ready.Task.ConfigureAwait(false);
            return new TuiTestHarness(app, mainWindow, stopCts, runTask);
        }
        catch
        {
            stopCts.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Polls the rendered screen (as one string per row) until some row contains every one of
    /// <paramref name="requiredSubstrings"/>, or throws <see cref="TimeoutException"/> after
    /// <see cref="PollTimeout"/>. Background indexing completes asynchronously, so callers must
    /// not assert immediately.
    /// </summary>
    public async Task<string[]> WaitForContentsAsync(params string[] requiredSubstrings)
    {
        ArgumentOutOfRangeException.ThrowIfZero(requiredSubstrings.Length);

        using var timeoutCts = new CancellationTokenSource(PollTimeout);
        try
        {
            var lines = await ReadContentsAsync().WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            while (!ContainsAllSubstrings(lines, requiredSubstrings))
            {
                await Task.Delay(PollInterval, timeoutCts.Token).ConfigureAwait(false);
                lines = await ReadContentsAsync().WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }

            return lines;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException($"Screen content did not satisfy the condition within {PollTimeout}.");
        }
    }

    private static bool ContainsAllSubstrings(string[] lines, string[] requiredSubstrings)
    {
        return lines.Any(line => requiredSubstrings.All(substring => line.Contains(substring, StringComparison.Ordinal)));
    }

    public async ValueTask DisposeAsync()
    {
        await _stopCts.CancelAsync().ConfigureAwait(false);

        // Cancel and wait only. The worker task (see StartAsync) owns app/mainWindow disposal
        // and DisableRealDriverIO restoration in its own finally, once RunAsync actually returns,
        // so this side never races a loop thread that may still be running.
        using var timeoutCts = new CancellationTokenSource(StopTimeout);
        try
        {
            await _runTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"TUI event loop did not stop within {StopTimeout} after cancellation; the worker thread still owns cleanup.");
        }

        _stopCts.Dispose();
    }

    /// <summary>
    /// Captures a screen snapshot from inside an <see cref="IApplication.Invoke(Action)"/>
    /// callback so the read happens on the UI thread instead of racing its draw calls.
    /// </summary>
    private Task<string[]> ReadContentsAsync()
    {
        var snapshot = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        _app.Invoke(() =>
        {
            try
            {
                snapshot.SetResult(CaptureContents());
            }
            catch (Exception ex)
            {
                snapshot.SetException(ex);
            }
        });

        return snapshot.Task;
    }

    private string[] CaptureContents()
    {
        var driver = _app.Driver ?? throw new InvalidOperationException("IApplication.Driver was not set after Init.");
        var contents = driver.Contents ?? throw new InvalidOperationException("IDriver.Contents was not initialized.");
        var rows = contents.GetLength(0);
        var cols = contents.GetLength(1);
        var lines = new string[rows];
        for (var row = 0; row < rows; row++)
        {
            var builder = new StringBuilder(cols);
            for (var col = 0; col < cols; col++)
            {
                builder.Append(contents[row, col].Grapheme);
            }

            lines[row] = builder.ToString();
        }

        return lines;
    }
}

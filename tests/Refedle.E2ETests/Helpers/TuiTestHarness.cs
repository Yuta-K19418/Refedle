using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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
    private readonly ConcurrentQueue<KeyCode> _pendingKeys = new();
    private readonly ConcurrentQueue<UiRequest> _pendingReads = new();
    private int _iterationPumpAttached;

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
    /// Queues a key press for delivery on the TUI event loop's next iteration, where it is raised
    /// through <see cref="IApplication.Keyboard"/>. Iteration events fire from whichever run loop is
    /// currently pumping — including the nested loop of a modal dialog opened by an earlier key — so
    /// dialogs can be driven key by key; marshaling through <see cref="IApplication.Invoke(Action)"/>
    /// instead stalls once a key opens a modal dialog, because the nested run loop never drains the
    /// queued callback that would unblock the caller. Deliberately fire-and-forget: observe effects
    /// through <see cref="WaitForContentsAsync"/> or <see cref="CaptureAsync"/> instead.
    /// </summary>
    /// <param name="keyCode">The key to press, without modifiers unless required.</param>
    public void SendKey(KeyCode keyCode)
    {
        AttachIterationPump();
        _pendingKeys.Enqueue(keyCode);
    }

    /// <summary>
    /// Queues one <see cref="SendKey"/> call per character in <paramref name="text"/>, in order.
    /// Only covers plain ASCII characters. Uppercase letters are sent with
    /// <see cref="KeyCode.ShiftMask"/>, since a bare uppercase-letter <see cref="KeyCode"/> value
    /// collides with that letter's unshifted key code and would otherwise type lowercase.
    /// </summary>
    public void SendText(string text)
    {
        foreach (var c in text)
        {
            var keyCode = (KeyCode)c;
            if (char.IsUpper(c))
            {
                keyCode |= KeyCode.ShiftMask;
            }

            SendKey(keyCode);
        }
    }

    /// <summary>
    /// Captures a single screen snapshot (one string per row) on the UI thread, without polling.
    /// </summary>
    public Task<string[]> CaptureAsync()
    {
        return ReadContentsAsync();
    }

    /// <summary>
    /// Reads the type name of the currently focused view on the UI thread. Diagnostic aid
    /// for key-driven tests whose view-level keys require focus.
    /// </summary>
    public Task<string?> ReadFocusedViewNameAsync()
    {
        return RequestUiReadAsync(() =>
            (_app.Navigation?.GetFocused() ?? _app.TopRunnableView?.MostFocused)?.GetType().FullName);
    }

    /// <summary>
    /// Finds the row/column of the top-left-most cell whose rendered attribute matches
    /// MorphTableView/MorphTreeView's selection highlight (black-on-white, the inverse of the
    /// default color scheme), or <see langword="null"/> if nothing is currently highlighted.
    /// The highlighted region always starts at this cell, so its position alone identifies which
    /// row (and, in a table, which column) is selected without polling.
    /// </summary>
    public Task<(int Row, int Col)?> GetSelectedCellAsync()
    {
        return RequestUiReadAsync(FindSelectedCell);
    }

    /// <summary>
    /// Polls <see cref="GetSelectedCellAsync"/> until it satisfies <paramref name="predicate"/>,
    /// or throws <see cref="TimeoutException"/> after <see cref="PollTimeout"/>. Lets vim-key
    /// navigation tests assert the exact cursor position a key is expected to produce, rather
    /// than only the rendered viewport content.
    /// </summary>
    public Task<(int Row, int Col)?> WaitForSelectedCellAsync(Func<(int Row, int Col)?, bool> predicate)
    {
        return PollAsync(GetSelectedCellAsync, predicate, "Selected cell");
    }

    /// <summary>
    /// Waits for the TUI event loop to stop — e.g. after a 'q' quit key —
    /// within <see cref="StopTimeout"/>.
    /// </summary>
    public async Task WaitForExitAsync()
    {
        using var timeoutCts = new CancellationTokenSource(StopTimeout);
        try
        {
            await _runTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException($"The TUI event loop did not stop within {StopTimeout}.");
        }
    }

    /// <summary>
    /// Creates the application, initializes it on the ANSI driver, fixes the screen to 80x24,
    /// subscribes key handling, and starts the real event loop — all on one dedicated background
    /// thread — so key handling, background indexing, and drawing all run for real.
    /// </summary>
    /// <remarks>
    /// Terminal.Gui records the thread that calls <c>Init</c> as its UI thread
    /// (<see cref="IApplication.MainThreadId"/>) and requires the event loop to be pumped from that
    /// same thread, so everything from <c>Init</c> onward runs on one dedicated worker thread.
    /// Despite its name,
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
    public Task<string[]> WaitForContentsAsync(params string[] requiredSubstrings)
    {
        ArgumentOutOfRangeException.ThrowIfZero(requiredSubstrings.Length);

        return WaitForConditionAsync(
            lines => ContainsAllSubstrings(lines, requiredSubstrings),
            $"Screen content containing [{string.Join(", ", requiredSubstrings)}]");
    }

    /// <summary>
    /// Polls the rendered screen (as one string per row) until <paramref name="predicate"/>
    /// returns <see langword="true"/>, or throws <see cref="TimeoutException"/> after
    /// <see cref="PollTimeout"/>. Use this instead of a fixed delay when the awaited transition
    /// isn't expressible as "some row contains this substring" — e.g. a header or value that
    /// disappears rather than appears, as <see cref="WaitForContentsAsync"/> can only wait for
    /// substrings to show up, not for them to be gone.
    /// </summary>
    public Task<string[]> WaitForConditionAsync(Func<string[], bool> predicate, string description)
    {
        return PollAsync(ReadContentsAsync, predicate, description);
    }

    private static bool ContainsAllSubstrings(string[] lines, string[] requiredSubstrings)
    {
        return lines.Any(line => requiredSubstrings.All(substring => line.Contains(substring, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Shared polling loop backing <see cref="WaitForConditionAsync"/> and
    /// <see cref="WaitForSelectedCellAsync"/>: repeatedly awaits <paramref name="read"/> until
    /// <paramref name="predicate"/> is satisfied, or throws <see cref="TimeoutException"/> after
    /// <see cref="PollTimeout"/>.
    /// </summary>
    private static async Task<T> PollAsync<T>(Func<Task<T>> read, Func<T, bool> predicate, string description)
    {
        using var timeoutCts = new CancellationTokenSource(PollTimeout);
        try
        {
            var value = await read().WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            while (!predicate(value))
            {
                await Task.Delay(PollInterval, timeoutCts.Token).ConfigureAwait(false);
                value = await read().WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }

            return value;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException($"{description} did not satisfy the condition within {PollTimeout}.");
        }
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
    /// Captures a screen snapshot from inside an iteration callback so the read happens on the UI
    /// thread instead of racing its draw calls. Iteration events also fire while a modal dialog's
    /// nested run loop is pumping, so snapshots work with dialogs open.
    /// </summary>
    private Task<string[]> ReadContentsAsync()
    {
        return RequestUiReadAsync(CaptureContents);
    }

    /// <summary>
    /// Queues a read of UI-thread state for execution on the next iteration of whichever run loop
    /// is currently pumping, and returns a task that completes with its result.
    /// </summary>
    private Task<T> RequestUiReadAsync<T>(Func<T> read)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        AttachIterationPump();
        _pendingReads.Enqueue(new UiRequest<T>(read, completion));
        return completion.Task;
    }

    /// <summary>
    /// Subscribes the iteration pump exactly once. Iteration events are the delivery channel for
    /// both queued keys and queued UI-state reads; the event-add is thread-safe, so racing the
    /// first loop iterations is harmless (the pump simply picks the queue up one iteration later).
    /// </summary>
    private void AttachIterationPump()
    {
        if (Interlocked.Exchange(ref _iterationPumpAttached, 1) == 1)
        {
            return;
        }

        _app.Iteration += (_, _) => PumpQueuedWork();
    }

    private void PumpQueuedWork()
    {
        // Deliver at most one key per iteration: a key that opens a modal dialog blocks here inside
        // a nested run loop, and spacing keys one iteration apart keeps each dialog step observable
        // before the next key arrives.
        if (_pendingKeys.TryDequeue(out var key))
        {
            _app.Keyboard.RaiseKeyDownEvent(key);
        }

        while (_pendingReads.TryDequeue(out var request))
        {
            request.Complete();
        }
    }

    private abstract class UiRequest
    {
        public abstract void Complete();
    }

    private sealed class UiRequest<T>(Func<T> read, TaskCompletionSource<T> completion) : UiRequest
    {
        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Marshals any exception from the UI-thread read back to the awaiting caller via TaskCompletionSource.")]
        public override void Complete()
        {
            try
            {
                completion.TrySetResult(read());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }
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

    /// <summary>
    /// Scans the driver's cell grid for the selection highlight (black-on-white) and returns the
    /// start of the first single contiguous highlighted span, in row-major order. A table's
    /// column header row uses the same black-on-white styling across every column, so it always
    /// renders as two or more separate spans (one per column, split by the unstyled border
    /// character between them); only the actual selected cell renders as exactly one span,
    /// whether that's one table column's width or a tree row's whole line.
    /// </summary>
    private (int Row, int Col)? FindSelectedCell()
    {
        var driver = _app.Driver ?? throw new InvalidOperationException("IApplication.Driver was not set after Init.");
        var contents = driver.Contents ?? throw new InvalidOperationException("IDriver.Contents was not initialized.");
        var rows = contents.GetLength(0);
        var cols = contents.GetLength(1);
        for (var row = 0; row < rows; row++)
        {
            var spanCount = 0;
            var firstSpanCol = -1;
            var inSpan = false;
            for (var col = 0; col < cols; col++)
            {
                var isHighlighted = IsSelectionHighlight(contents[row, col].Attribute);
                if (isHighlighted && !inSpan)
                {
                    spanCount++;
                    firstSpanCol = firstSpanCol < 0 ? col : firstSpanCol;
                }

                inSpan = isHighlighted;
            }

            if (spanCount == 1)
            {
                return (row, firstSpanCol);
            }
        }

        return null;
    }

    private static bool IsSelectionHighlight(Terminal.Gui.Drawing.Attribute? attribute) =>
        attribute is { } value
        && value.Foreground == Terminal.Gui.Drawing.Color.Black
        && value.Background == Terminal.Gui.Drawing.Color.White;
}

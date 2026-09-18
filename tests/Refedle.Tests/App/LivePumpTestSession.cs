using Refedle.App;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace Refedle.Tests.App;

/// <summary>
/// Bundles the AppState/ViewManager/handler a live-pump test needs, built on the session's worker
/// thread. Disposing releases the ViewManager and AppState.
/// </summary>
internal sealed record LiveTestContext<THandler>(AppState State, ViewManager ViewManager, THandler Handler) : IDisposable
{
    public void Dispose()
    {
        ViewManager.Dispose();
        State.Dispose();
    }
}

/// <summary>
/// Factory for <see cref="LivePumpTestSession{T}"/>, giving callers type inference on the
/// setup delegate's return type.
/// </summary>
internal static class LivePumpTestSession
{
    public static Task<LivePumpTestSession<T>> StartAsync<T>(Func<IApplication, Window, T> setup) =>
        LivePumpTestSession<T>.StartAsync(setup);
}

/// <summary>
/// Test-only Terminal.Gui session where Init, setup, the running loop, and disposal are all owned
/// by one dedicated worker thread — including on setup/readiness failure — matching
/// tests/Refedle.E2ETests/Helpers/TuiTestHarness.cs. Test code cannot reach the application,
/// window, or setup objects directly: every touch is marshalled onto the worker thread through
/// <see cref="InvokeAsync{TResult}"/>, which reports completion back through a
/// RunContinuationsAsynchronously TaskCompletionSource, so no test can dereference a UI-owned
/// object outside the loop or after the session has stopped.
/// </summary>
internal sealed class LivePumpTestSession<T> : IAsyncDisposable
{
    // Bounds every wait on the pump so a synchronization regression fails the test with a
    // diagnosable timeout instead of hanging CI indefinitely.
    private static readonly TimeSpan _pumpTimeout = TimeSpan.FromSeconds(15);

    private readonly IApplication _app;
    private readonly T _setup;
    private readonly CancellationTokenSource _cts;
    private readonly Task _pumpTask;

    private LivePumpTestSession(IApplication app, T setup, CancellationTokenSource cts, Task pumpTask)
    {
        _app = app;
        _setup = setup;
        _cts = cts;
        _pumpTask = pumpTask;
    }

    public static async Task<LivePumpTestSession<T>> StartAsync(Func<IApplication, Window, T> setup)
    {
        var initTcs = new TaskCompletionSource<(IApplication App, T Setup)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var readyTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cts = new CancellationTokenSource();

        var pumpTask = Task.Run(async () =>
        {
            IApplication? app = null;
            Window? window = null;
            T? setupResult = default;

            try
            {
                app = Application.Create();
                app.Init(DriverRegistry.Names.ANSI);
                Assert.NotNull(app.Driver);
                app.Driver.SetScreenSize(80, 25);
                window = new Window();
                setupResult = setup(app, window);

                void OnFirstIteration(object? sender, EventArgs<IApplication?> args)
                {
                    app.Iteration -= OnFirstIteration;
                    readyTcs.TrySetResult();
                }

                app.Iteration += OnFirstIteration;
                initTcs.SetResult((app, setupResult));

                await app.RunAsync(window, cts.Token);
            }
            catch (Exception ex)
            {
                initTcs.TrySetException(ex);
                readyTcs.TrySetException(ex);
                throw;
            }
            finally
            {
                if (setupResult is IDisposable disposableSetup)
                {
                    disposableSetup.Dispose();
                }

                window?.Dispose();
                app?.Dispose();
            }
        });

        try
        {
            var (app, result) = await AwaitWithTimeoutAsync(initTcs.Task, "the worker to initialize the application");
            await AwaitWithTimeoutAsync(readyTcs.Task, "the loop's first iteration");

            return new LivePumpTestSession<T>(app, result, cts, pumpTask);
        }
        catch
        {
            await CancelAndObserveAsync(cts, pumpTask);
            throw;
        }
    }

    public Task InvokeAsync(Func<IApplication, T, Task> action) =>
        InvokeAsync<object?>(async (app, setup) =>
        {
            await action(app, setup);
            return null;
        });

    public async Task<TResult> InvokeAsync<TResult>(Func<IApplication, T, Task<TResult>> action)
    {
        var startedTcs = new TaskCompletionSource<Task<TResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _app.Invoke(() =>
        {
            try
            {
                startedTcs.TrySetResult(action(_app, _setup));
            }
            catch (Exception ex)
            {
                startedTcs.TrySetException(ex);
            }
        });

        // _app.Invoke only enqueues the callback and never signals completion, so the loop
        // thread starting the action and the action itself finishing are tracked as two
        // separate awaits rather than one.
        var task = await AwaitWithTimeoutAsync(startedTcs.Task, "Invoke to start the marshalled action on the loop thread");
        return await AwaitWithTimeoutAsync(task, "the marshalled action to complete");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _cts.Cancel();
            await AwaitWithTimeoutAsync(_pumpTask, "the pump to shut down after cancellation");
        }
        finally
        {
            _cts.Dispose();
        }
    }

    private static async Task<TResult> AwaitWithTimeoutAsync<TResult>(Task<TResult> task, string operation)
    {
        try
        {
            return await task.WaitAsync(_pumpTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException(
                $"LivePumpTestSession timed out after {_pumpTimeout} waiting for: {operation}.", ex);
        }
    }

    private static async Task AwaitWithTimeoutAsync(Task task, string operation)
    {
        try
        {
            await task.WaitAsync(_pumpTimeout);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException(
                $"LivePumpTestSession timed out after {_pumpTimeout} waiting for: {operation}.", ex);
        }
    }

    private static async Task CancelAndObserveAsync(CancellationTokenSource cts, Task pumpTask)
    {
        try
        {
            cts.Cancel();

            try
            {
                await pumpTask.WaitAsync(_pumpTimeout);
            }
            catch
            {
                // Intentionally ignored — see method-level justification above.
            }
        }
        finally
        {
            cts.Dispose();
        }
    }
}

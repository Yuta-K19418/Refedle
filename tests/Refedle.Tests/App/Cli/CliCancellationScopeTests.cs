using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

public sealed class CliCancellationScopeTests
{
    [Fact]
    public void Create_WhenOuterTokenIsCancelled_CancelsScopeToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var scope = CliCancellationScope.Create(new TestCancelKeyPressSource(), cts.Token);

        // Act
        cts.Cancel();

        // Assert
        scope.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenCtrlCIsPressed_CancelsScopeToken()
    {
        // Arrange
        var source = new TestCancelKeyPressSource();
        using var scope = CliCancellationScope.Create(source, CancellationToken.None);

        // Act
        var args = source.Raise();

        // Assert
        scope.Token.IsCancellationRequested.Should().BeTrue();
        args.Cancel.Should().BeTrue();
    }

    [Fact]
    public void Dispose_WhenCalledTwice_DoesNotThrow()
    {
        // Arrange
        var scope = CliCancellationScope.Create(new TestCancelKeyPressSource(), CancellationToken.None);
        scope.Dispose();

        // Act
        var act = scope.Dispose;

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_ThenCtrlCPressed_DoesNotThrowAndSourceHasNoSubscribers()
    {
        // Arrange — a disposed scope has unsubscribed, so a later Ctrl+C press must not reach it.
        var source = new TestCancelKeyPressSource();
        var scope = CliCancellationScope.Create(source, CancellationToken.None);
        scope.Dispose();

        // Act
        var act = source.Raise;

        // Assert
        act.Should().NotThrow();
        source.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public async Task Dispose_WhileHandlerInFlight_DoesNotThrowAndDoesNotCancelDisposedSource()
    {
        // Arrange — the blocking subscriber is registered before the scope, so it runs first
        // and keeps the raise in flight on a worker thread while the scope's own handler has
        // not been entered yet.
        var source = new TestCancelKeyPressSource();
        using var handlerEntered = new ManualResetEventSlim(false);
        using var releaseHandler = new ManualResetEventSlim(false);
        source.CancelKeyPress += (_, _) =>
        {
            handlerEntered.Set();
            releaseHandler.Wait();
        };
        using var scope = CliCancellationScope.Create(source, CancellationToken.None);
        var cancelled = false;
        using var registration = scope.Token.Register(() => cancelled = true);
        var raiseTask = Task.Run(source.Raise);
        try
        {
            handlerEntered.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

            // Act — Dispose while the raise is in flight, then let the paused handler resume.
            scope.Dispose();
            releaseHandler.Set();
            await raiseTask;

            // Assert — a disposal exception, or the released handler cancelling the disposed
            // source (an ObjectDisposedException rethrown by the await above), fails the test.
            cancelled.Should().BeFalse();
        }
        finally
        {
            // On every failure path, release the worker and observe its outcome before the
            // using declarations dispose the ManualResetEventSlim instances it still uses.
            releaseHandler.Set();
            await raiseTask;
        }
    }

    [Fact]
    public void Create_Dispose_Create_ThenCtrlCPressed_OnlyLiveScopeCancels_AndExactlyOneSubscriber()
    {
        // Arrange — a disposed scope must not leave its handler subscribed to the shared source.
        var source = new TestCancelKeyPressSource();
        var deadScope = CliCancellationScope.Create(source, CancellationToken.None);
        var deadToken = deadScope.Token;
        deadScope.Dispose();
        var liveScope = CliCancellationScope.Create(source, CancellationToken.None);

        // Act
        source.Raise();

        // Assert
        liveScope.Token.IsCancellationRequested.Should().BeTrue();
        deadToken.IsCancellationRequested.Should().BeFalse();
        source.SubscriberCount.Should().Be(1);
        liveScope.Dispose();
        source.SubscriberCount.Should().Be(0);
    }

    private sealed class TestCancelKeyPressSource : ICancelKeyPressSource
    {
        private ConsoleCancelEventHandler? _handlers;

        public event ConsoleCancelEventHandler? CancelKeyPress
        {
            add => _handlers += value;
            remove => _handlers -= value;
        }

        public int SubscriberCount => _handlers?.GetInvocationList().Length ?? 0;

        public ConsoleCancelEventArgs Raise()
        {
            // ConsoleCancelEventArgs is sealed with an internal constructor (only Console can
            // create it), so a zero-initialized instance stands in for the real signal payload.
            var args = (ConsoleCancelEventArgs)RuntimeHelpers.GetUninitializedObject(typeof(ConsoleCancelEventArgs));
            _handlers?.Invoke(this, args);
            return args;
        }
    }
}

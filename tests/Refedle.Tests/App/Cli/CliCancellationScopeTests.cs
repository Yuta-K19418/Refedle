using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

public sealed class CliCancellationScopeTests
{
    private static readonly TimeSpan _waitTimeout = TimeSpan.FromMinutes(1);

    [Fact]
    public void Create_WhenOuterTokenIsCancelled_CancelsScopeToken()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        using CliCancellationScope scope = CliCancellationScope.Create(new TestCancelKeyPressSource(), cts.Token);

        // Act
        cts.Cancel();

        // Assert
        scope.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenCtrlCIsPressed_CancelsScopeToken()
    {
        // Arrange
        TestCancelKeyPressSource source = new();
        using CliCancellationScope scope = CliCancellationScope.Create(source, CancellationToken.None);

        // Act
        ConsoleCancelEventArgs args = source.Raise();

        // Assert
        scope.Token.IsCancellationRequested.Should().BeTrue();
        args.Cancel.Should().BeTrue();
    }

    [Fact]
    public void Dispose_WhenCalledTwice_DoesNotThrow()
    {
        // Arrange
        CliCancellationScope scope = CliCancellationScope.Create(new TestCancelKeyPressSource(), CancellationToken.None);
        scope.Dispose();

        // Act
        Action act = scope.Dispose;

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_ThenCtrlCPressed_DoesNotThrowAndSourceHasNoSubscribers()
    {
        // Arrange — a disposed scope has unsubscribed, so a later Ctrl+C press must not reach it.
        TestCancelKeyPressSource source = new();
        CliCancellationScope scope = CliCancellationScope.Create(source, CancellationToken.None);
        scope.Dispose();

        // Act
        Func<ConsoleCancelEventArgs> act = source.Raise;

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
        TestCancelKeyPressSource source = new();
        TaskCompletionSource handlerEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim releaseHandler = new(false);
        source.CancelKeyPress += (_, _) =>
        {
            handlerEntered.SetResult();
            releaseHandler.Wait();
        };
        using CliCancellationScope scope = CliCancellationScope.Create(source, CancellationToken.None);
        bool cancelled = false;
        using CancellationTokenRegistration registration = scope.Token.Register(() => cancelled = true);
        Task<ConsoleCancelEventArgs> raiseTask = Task.Run(source.Raise);
        try
        {
            await handlerEntered.Task.WaitAsync(_waitTimeout);

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
            // using declarations dispose the ManualResetEventSlim it still uses.
            releaseHandler.Set();
            await raiseTask;
        }
    }

    [Fact]
    public void Create_Dispose_Create_ThenCtrlCPressed_OnlyLiveScopeCancels_AndExactlyOneSubscriber()
    {
        // Arrange — a disposed scope must not leave its handler subscribed to the shared source.
        TestCancelKeyPressSource source = new();
        CliCancellationScope deadScope = CliCancellationScope.Create(source, CancellationToken.None);
        CancellationToken deadToken = deadScope.Token;
        deadScope.Dispose();
        CliCancellationScope liveScope = CliCancellationScope.Create(source, CancellationToken.None);

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
            ConsoleCancelEventArgs args = (ConsoleCancelEventArgs)RuntimeHelpers.GetUninitializedObject(typeof(ConsoleCancelEventArgs));
            _handlers?.Invoke(this, args);
            return args;
        }
    }
}

using AwesomeAssertions;
using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

public sealed class DeferredAppLoggerTests
{
    [Fact]
    public async Task WriteAsync_BeforeFlush_DoesNotReachTarget()
    {
        // Arrange
        var deferred = new DeferredAppLogger();
        var target = new TestAppLogger();

        // Act
        await deferred.WriteInfoAsync("info");
        await deferred.WriteWarningAsync("warning");
        await deferred.WriteErrorAsync("error");

        // Assert
        target.LogCount.Should().Be(0);
    }

    [Fact]
    public async Task FlushToAsync_ReplaysMessagesToMatchingLevelsInWrittenOrder()
    {
        // Arrange
        var deferred = new DeferredAppLogger();
        var target = new OrderRecordingLogger();
        await deferred.WriteInfoAsync("i1");
        await deferred.WriteErrorAsync("e1");
        await deferred.WriteWarningAsync("w1");
        await deferred.WriteInfoAsync("i2");

        // Act
        await deferred.FlushToAsync(target);

        // Assert
        target.Entries.Should().Equal("info:i1", "error:e1", "warning:w1", "info:i2");
    }

    [Fact]
    public async Task FlushToAsync_CalledTwice_ReplaysEachMessageOnlyOnce()
    {
        // Arrange
        var deferred = new DeferredAppLogger();
        var target = new TestAppLogger();
        await deferred.WriteInfoAsync("only once");

        // Act
        await deferred.FlushToAsync(target);
        await deferred.FlushToAsync(target);

        // Assert
        target.Infos.Should().Equal("only once");
    }

    [Fact]
    public async Task FlushToAsync_WithNoMessages_WritesNothing()
    {
        // Arrange
        var deferred = new DeferredAppLogger();
        var target = new TestAppLogger();

        // Act
        await deferred.FlushToAsync(target);

        // Assert
        target.LogCount.Should().Be(0);
    }

    [Fact]
    public async Task FlushToAsync_WithNullTarget_ThrowsArgumentNullException()
    {
        // Arrange
        var deferred = new DeferredAppLogger();

        // Act
        var act = async () => await deferred.FlushToAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task WriteAsync_FromConcurrentThreads_KeepsEveryMessage()
    {
        // Arrange
        const int writers = 8;
        const int perWriter = 200;
        var deferred = new DeferredAppLogger();
        var target = new TestAppLogger();
        using var startGate = new Barrier(writers);

        // Act
        var tasks = Enumerable.Range(0, writers)
            .Select(writer => Task.Factory.StartNew(
                () => WriteFromWriter(deferred, startGate, writer, perWriter),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();
        await Task.WhenAll(tasks);
        await deferred.FlushToAsync(target);

        // Assert
        target.Infos.Should().HaveCount(writers * perWriter).And.OnlyHaveUniqueItems();
    }

    // Blocking on the barrier lets every writer start together so the lock is actually contended.
    private static void WriteFromWriter(DeferredAppLogger deferred, Barrier startGate, int writer, int count)
    {
        startGate.SignalAndWait();
        for (var i = 0; i < count; i++)
        {
            deferred.WriteInfoAsync($"{writer}-{i}").AsTask().GetAwaiter().GetResult();
        }
    }

    private sealed class OrderRecordingLogger : IAppLogger
    {
        private readonly List<string> _entries = [];

        public IReadOnlyList<string> Entries => _entries.AsReadOnly();

        public ValueTask WriteInfoAsync(string message) => Record("info", message);

        public ValueTask WriteWarningAsync(string message) => Record("warning", message);

        public ValueTask WriteErrorAsync(string message) => Record("error", message);

        private ValueTask Record(string level, string message)
        {
            _entries.Add($"{level}:{message}");
            return ValueTask.CompletedTask;
        }
    }
}

using AwesomeAssertions;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO;

namespace Refedle.Tests.App.Tui.Workers;

public sealed class IndexingCompletionTests
{
    [Fact]
    public async Task WhenCompletedAsync_WithAlreadyCompletedIndexer_ReturnsCompletedTask()
    {
        // Arrange
        FakeRowIndexer indexer = new() { IsIndexingCompleted = true };

        // Act
        Task task = IndexingCompletion.WhenCompletedAsync(indexer);

        // Assert
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task WhenCompletedAsync_WithRunningIndexer_StaysPendingUntilBuildIndexCompleted()
    {
        // Arrange
        FakeRowIndexer indexer = new();
        Task task = IndexingCompletion.WhenCompletedAsync(indexer);
        bool pendingBeforeRaise = !task.IsCompleted;

        // Act
        indexer.RaiseBuildIndexCompleted();
        await task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        pendingBeforeRaise.Should().BeTrue();
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task WhenCompletedAsync_WhenCompletionOccursDuringStatusCheck_ReturnsCompletedTask()
    {
        // Arrange
        FakeRowIndexer indexer = new() { RaiseCompletionDuringStatusCheck = true };

        // Act
        Task task = IndexingCompletion.WhenCompletedAsync(indexer);

        // Assert
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task WhenCompletedAsync_WhenBuildIndexCompletedRaisedTwice_CompletesWithoutThrowing()
    {
        // Arrange
        FakeRowIndexer indexer = new();
        Task task = IndexingCompletion.WhenCompletedAsync(indexer);

        // Act
        indexer.RaiseBuildIndexCompleted();
        Action act = indexer.RaiseBuildIndexCompleted;

        // Assert
        act.Should().NotThrow();
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void WhenCompletedAsync_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Act
        // A statement body keeps the lambda Action-returning, so Throw asserts a synchronous throw.
        Action act = () =>
        {
            IndexingCompletion.WhenCompletedAsync(null!);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("indexer");
    }

    [Fact]
    public async Task WhenCompletedAsync_WithAlreadyCompletedIndexer_RemovesCompletionSubscription()
    {
        // Arrange
        FakeRowIndexer indexer = new() { IsIndexingCompleted = true };

        // Act
        Task task = IndexingCompletion.WhenCompletedAsync(indexer);

        // Assert — the status-check path detaches the handler even though it never fired
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        indexer.BuildIndexCompletedSubscribers.Should().Be(0);
    }

    [Fact]
    public async Task WhenCompletedAsync_WithRunningIndexer_RemovesSubscriptionWhenIndexingCompletes()
    {
        // Arrange
        FakeRowIndexer indexer = new();
        Task task = IndexingCompletion.WhenCompletedAsync(indexer);

        // Act
        indexer.RaiseBuildIndexCompleted();

        // Assert — the handler detached itself when the completion event fired
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        indexer.BuildIndexCompletedSubscribers.Should().Be(0);
    }

    [Fact]
    public async Task WhenCompletedAsync_WhenCalledRepeatedly_LeavesNoSubscriptionsAfterCompletion()
    {
        // Arrange
        FakeRowIndexer indexer = new();
        Task[] tasks = [.. Enumerable.Range(0, 3).Select(_ => IndexingCompletion.WhenCompletedAsync(indexer))];
        int subscribersWhileRunning = indexer.BuildIndexCompletedSubscribers;

        // Act
        indexer.RaiseBuildIndexCompleted();

        // Assert — every call completed and none of the handlers accumulated
        subscribersWhileRunning.Should().Be(3);
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
        tasks.Should().OnlyContain(t => t.IsCompletedSuccessfully);
        indexer.BuildIndexCompletedSubscribers.Should().Be(0);
    }

    [Fact]
    public async Task WhenCompletedAsync_WhenEventAndStatusCheckBothReportCompletion_DetachesOnce()
    {
        // Arrange — the status check raises the completion event and also reports completion,
        // so the handler and the check both run the detach-and-complete path
        FakeRowIndexer indexer = new() { RaiseCompletionDuringStatusCheck = true, IsIndexingCompleted = true };

        // Act
        Task task = IndexingCompletion.WhenCompletedAsync(indexer);

        // Assert — the double invocation is idempotent: one completion, no leftover handler
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        task.IsCompletedSuccessfully.Should().BeTrue();
        indexer.BuildIndexCompletedSubscribers.Should().Be(0);
    }

    private sealed class FakeRowIndexer : IRowIndexer
    {
#pragma warning disable CS0067
        public event Action? FirstCheckpointReached;
        public event Action<long, long>? ProgressChanged;
#pragma warning restore CS0067

        private Action? _buildIndexCompleted;
        private bool _isIndexingCompleted;

        public event Action? BuildIndexCompleted
        {
            add => _buildIndexCompleted += value;
            remove => _buildIndexCompleted -= value;
        }

        public long BytesRead => 0;
        public long FileSize => 0;
        public long TotalRows => 0;
        public string FilePath => string.Empty;
        public bool RaiseCompletionDuringStatusCheck { get; init; }

        /// <summary>Number of handlers currently subscribed to the completion event.</summary>
        public int BuildIndexCompletedSubscribers => _buildIndexCompleted?.GetInvocationList().Length ?? 0;

        public bool IsIndexingCompleted
        {
            get
            {
                if (RaiseCompletionDuringStatusCheck)
                {
                    RaiseBuildIndexCompleted();
                }

                return _isIndexingCompleted;
            }

            set => _isIndexingCompleted = value;
        }

        public void RaiseBuildIndexCompleted() => _buildIndexCompleted?.Invoke();

        public void BuildIndex(CancellationToken ct = default)
        {
        }

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => (0, 0);
    }
}

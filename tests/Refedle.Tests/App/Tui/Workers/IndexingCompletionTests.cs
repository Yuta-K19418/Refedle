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
        var indexer = new FakeRowIndexer { IsIndexingCompleted = true };

        // Act
        var task = IndexingCompletion.WhenCompletedAsync(indexer);

        // Assert
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task WhenCompletedAsync_WithRunningIndexer_StaysPendingUntilBuildIndexCompleted()
    {
        // Arrange
        var indexer = new FakeRowIndexer();
        var task = IndexingCompletion.WhenCompletedAsync(indexer);
        var pendingBeforeRaise = !task.IsCompleted;

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
        var indexer = new FakeRowIndexer { RaiseCompletionDuringStatusCheck = true };

        // Act
        var task = IndexingCompletion.WhenCompletedAsync(indexer);

        // Assert
        await task.WaitAsync(TimeSpan.FromSeconds(10));
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task WhenCompletedAsync_WhenBuildIndexCompletedRaisedTwice_CompletesWithoutThrowing()
    {
        // Arrange
        var indexer = new FakeRowIndexer();
        var task = IndexingCompletion.WhenCompletedAsync(indexer);

        // Act
        indexer.RaiseBuildIndexCompleted();
        var act = indexer.RaiseBuildIndexCompleted;

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
        var act = () =>
        {
            IndexingCompletion.WhenCompletedAsync(null!);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("indexer");
    }

    private sealed class FakeRowIndexer : IRowIndexer
    {
#pragma warning disable CS0067
        public event Action? FirstCheckpointReached;
        public event Action<long, long>? ProgressChanged;
#pragma warning restore CS0067
        public event Action? BuildIndexCompleted;

        public long BytesRead => 0;
        public long FileSize => 0;
        public long TotalRows => 0;
        public string FilePath => string.Empty;
        public bool RaiseCompletionDuringStatusCheck { get; init; }

        public bool IsIndexingCompleted
        {
            get
            {
                if (RaiseCompletionDuringStatusCheck)
                {
                    BuildIndexCompleted?.Invoke();
                    return false;
                }

                return field;
            }
            init;
        }

        public void RaiseBuildIndexCompleted() => BuildIndexCompleted?.Invoke();

        public void BuildIndex(CancellationToken ct = default)
        {
        }

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => (0, 0);
    }
}

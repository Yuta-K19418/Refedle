using AwesomeAssertions;
using Refedle.App.Tui.Workers;
using Refedle.Engine.Filtering;

namespace Refedle.Tests.App.Tui.Workers;

public sealed class FilterIndexingRunnerTests
{
    [Fact]
    public async Task RunAsync_WithValidFilterIndexer_InvokesBuildIndexAsyncWithToken()
    {
        // Arrange
        var indexer = new FakeFilterRowIndexer();
        using var cts = new CancellationTokenSource();

        // Act
        await FilterIndexingRunner.RunAsync(indexer, cts.Token);

        // Assert
        indexer.ObservedToken.Should().Be(cts.Token);
    }

    [Fact]
    public void RunAsync_WithNullFilterIndexer_ThrowsArgumentNullException()
    {
        // Arrange — no setup required

        // Act
        // A statement body keeps the lambda Action-returning, so Throw asserts a synchronous throw.
        var act = () =>
        {
            FilterIndexingRunner.RunAsync(null!, CancellationToken.None);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RunAsync_WithCancelledToken_CancelsWithoutInvokingIndexer()
    {
        // Arrange
        var indexer = new FakeFilterRowIndexer();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await FilterIndexingRunner.RunAsync(indexer, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        indexer.ObservedToken.Should().Be(CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_WhenIndexerFaults_PropagatesException()
    {
        // Arrange
        var indexer = new FaultingFilterRowIndexer();

        // Act
        var act = async () => await FilterIndexingRunner.RunAsync(indexer, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Records the token it was invoked with, so tests can verify it flows through
    /// <see cref="FilterIndexingRunner.RunAsync"/> unchanged.
    /// </summary>
    private sealed class FakeFilterRowIndexer : IFilterRowIndexer
    {
        public int TotalMatchedRows => 0;

        public CancellationToken ObservedToken { get; private set; }

        public int GetSourceRow(int filteredRow) => -1;

        public Task BuildIndexAsync(CancellationToken ct)
        {
            ObservedToken = ct;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Always fails its index build, so tests can verify the fault reaches
    /// <see cref="FilterIndexingRunner.RunAsync"/>'s returned task.
    /// </summary>
    private sealed class FaultingFilterRowIndexer : IFilterRowIndexer
    {
        public int TotalMatchedRows => 0;

        public int GetSourceRow(int filteredRow) => -1;

        public Task BuildIndexAsync(CancellationToken ct) =>
            Task.FromException(new InvalidOperationException("index build failed"));
    }
}

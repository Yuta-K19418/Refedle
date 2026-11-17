using AwesomeAssertions;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.App.Tui.Workers;

public sealed class IndexTaskManagerTests : IDisposable
{
    private static readonly TimeSpan _waitTimeout = TimeSpan.FromMinutes(1);

    private readonly string _testFilePath;

    public IndexTaskManagerTests()
    {
        _testFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public async Task Start_StartsIndexing()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, "{\"id\":1}\n{\"id\":2}");
        RowIndexer indexer = new(_testFilePath);
        using IndexTaskManager manager = new();
        TaskCompletionSource<bool> tcs = new();
        indexer.BuildIndexCompleted += () => tcs.TrySetResult(true);

        // Act
        manager.Start(indexer);

        // Assert
        await tcs.Task.WaitAsync(_waitTimeout);
        indexer.TotalRows.Should().Be(2);
    }

    [Fact]
    public void Start_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        using IndexTaskManager manager = new();

        // Act
        Action act = () => manager.Start(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        IndexTaskManager manager = new();
        manager.Dispose();

        // Act
        Action act = () => manager.Start(new RowIndexer(_testFilePath));

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        IndexTaskManager manager = new();

        // Act
        manager.Dispose();
        Action act = () => manager.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WithoutPriorStart_DoesNotThrow()
    {
        // Arrange
        IndexTaskManager manager = new();

        // Act
        Action act = () => manager.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Start_CalledTwice_CancelsPreviousTask()
    {
        // Arrange
        // Use BlockingIndexer for indexer1 to guarantee it is still running when
        // indexer2 starts, making the cancellation behaviour deterministic.
        BlockingIndexer indexer1 = new();
        TaskCompletionSource<bool> firstCheckpoint1 = new();
        TaskCompletionSource<bool> completed1 = new();
        indexer1.FirstCheckpointReached += () => firstCheckpoint1.TrySetResult(true);
        indexer1.BuildIndexCompleted += () => completed1.TrySetResult(true);

        List<string> lines = [.. Enumerable.Range(0, 500_000).Select(i => $"{{\"id\":{i}}}")];
        await File.WriteAllLinesAsync(_testFilePath, lines);

        RowIndexer indexer2 = new(_testFilePath);
        TaskCompletionSource<bool> completed2 = new();
        indexer2.BuildIndexCompleted += () => completed2.TrySetResult(true);

        using IndexTaskManager manager = new();

        // Act
        manager.Start(indexer1);

        // Wait until indexer1 is blocked inside BuildIndex (WaitOne), ensuring it
        // is still running when we start indexer2.
        await firstCheckpoint1.Task.WaitAsync(_waitTimeout);

        manager.Start(indexer2);

        // Assert
        await completed1.Task.WaitAsync(_waitTimeout);
        await completed2.Task.WaitAsync(_waitTimeout);

        indexer1.WasCancelled.Should().BeTrue();
        indexer2.TotalRows.Should().Be(500_000);
    }

    [Fact]
    public void CancelCurrent_WhenIdle_DoesNotThrow()
    {
        // Arrange
        using IndexTaskManager manager = new();

        // Act
        Action act = () => manager.CancelCurrent();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task CancelCurrent_AfterStart_CancelsTask()
    {
        // Arrange
        BlockingIndexer indexer = new();
        TaskCompletionSource<bool> firstCheckpoint = new();
        TaskCompletionSource<bool> completed = new();
        indexer.FirstCheckpointReached += () => firstCheckpoint.TrySetResult(true);
        indexer.BuildIndexCompleted += () => completed.TrySetResult(true);

        using IndexTaskManager manager = new();
        manager.Start(indexer);
        await firstCheckpoint.Task.WaitAsync(_waitTimeout);

        // Act
        manager.CancelCurrent();

        // Assert
        await completed.Task.WaitAsync(_waitTimeout);
        indexer.WasCancelled.Should().BeTrue();
    }

    [Fact]
    public void CancelCurrent_CalledTwice_DoesNotThrow()
    {
        // Arrange
        using IndexTaskManager manager = new();
        manager.CancelCurrent();

        // Act
        Action act = () => manager.CancelCurrent();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void CancelCurrent_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        IndexTaskManager manager = new();
        manager.Dispose();

        // Act
        Action act = () => manager.CancelCurrent();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_CancelsIndexing()
    {
        // Arrange
        // Use a controllable fake that blocks until cancelled, making the test
        // deterministic regardless of machine speed.
        BlockingIndexer indexer = new();
        TaskCompletionSource<bool> firstCheckpoint = new();
        TaskCompletionSource<bool> completed = new();
        indexer.FirstCheckpointReached += () => firstCheckpoint.TrySetResult(true);
        indexer.BuildIndexCompleted += () => completed.TrySetResult(true);

        IndexTaskManager manager = new();

        // Act
        manager.Start(indexer);

        // Wait until the indexer is blocked inside BuildIndex (WaitOne)
        await firstCheckpoint.Task.WaitAsync(_waitTimeout);

        manager.Dispose();

        // Assert
        await completed.Task.WaitAsync(_waitTimeout);
        indexer.WasCancelled.Should().BeTrue();
    }

    /// <summary>
    /// A fake indexer that fires FirstCheckpointReached immediately, then blocks
    /// until the CancellationToken is cancelled. This makes cancellation tests fully
    /// deterministic regardless of machine speed.
    /// </summary>
    private sealed class BlockingIndexer : IRowIndexer
    {
        public bool WasCancelled { get; private set; }

        public event Action? FirstCheckpointReached;
#pragma warning disable CS0067
        public event Action<long, long>? ProgressChanged;
#pragma warning restore CS0067
        public event Action? BuildIndexCompleted;

        public long BytesRead => 0;
        public long FileSize => 0;
        public long TotalRows => 0;
        public string FilePath => string.Empty;
        public bool IsIndexingCompleted { get; private set; }

        public void BuildIndex(CancellationToken ct = default)
        {
            FirstCheckpointReached?.Invoke();
            while (!ct.IsCancellationRequested)
            {
                Thread.Sleep(1);
            }

            WasCancelled = true;
            IsIndexingCompleted = true;
            BuildIndexCompleted?.Invoke();
        }

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => (0, 0);
    }
}

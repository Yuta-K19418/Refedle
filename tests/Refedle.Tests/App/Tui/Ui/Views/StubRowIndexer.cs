using Refedle.Engine.IO;

namespace Refedle.Tests.App.Tui.Ui.Views;

/// <summary>
/// Stub <see cref="IRowIndexer"/> that overrides <see cref="IRowIndexer.TotalRows"/> and
/// <see cref="IRowIndexer.IsIndexingCompleted"/> while delegating everything else to the inner indexer.
/// Supports manual event raising for testing progressive loading.
/// Shared by <see cref="JsonLinesTreeViewTests"/> and <see cref="JsonArrayTreeViewTests"/>.
/// </summary>
internal sealed class StubRowIndexer : IRowIndexer
{
    private readonly IRowIndexer _inner;
    private long _fakeTotalRows;
    private readonly bool? _fakeIsCompleted;
    private readonly long? _fakeFileSize;

    public StubRowIndexer(IRowIndexer inner, long fakeTotalRows, bool? fakeIsCompleted = null, long? fakeFileSize = null)
    {
        _inner = inner;
        _fakeTotalRows = fakeTotalRows;
        _fakeIsCompleted = fakeIsCompleted;
        _fakeFileSize = fakeFileSize;
    }

    public string FilePath => _inner.FilePath;
    public long FileSize => _fakeFileSize ?? _inner.FileSize;
    public long BytesRead => _inner.BytesRead;
    public long TotalRows => _fakeTotalRows;
    public bool IsIndexingCompleted => _fakeIsCompleted ?? _inner.IsIndexingCompleted;

    public void UpdateTotalRows(long totalRows) => _fakeTotalRows = totalRows;

#pragma warning disable CS0067
    public event Action? FirstCheckpointReached;
#pragma warning restore CS0067
    public event Action<long, long>? ProgressChanged;
    public event Action? BuildIndexCompleted;

    public void RaiseProgressChanged(long bytesRead, long fileSize) =>
        ProgressChanged?.Invoke(bytesRead, fileSize);

    public void RaiseBuildIndexCompleted() =>
        BuildIndexCompleted?.Invoke();

    public void BuildIndex(CancellationToken ct = default) => _inner.BuildIndex(ct);

    public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) =>
        _inner.GetCheckPoint(targetRow);
}

/// <summary>
/// Stub that returns <c>false</c> for <see cref="IRowIndexer.IsIndexingCompleted"/> for the first
/// <c>incompleteCallCount</c> calls, then <c>true</c> on all subsequent calls.
/// Used to test TOCTOU race conditions in the <c>JsonLinesTreeView.Create</c> and
/// <c>JsonArrayTreeView.Create</c> factory methods, where the indexer completes between the initial
/// check and the re-check.
/// </summary>
internal sealed class ToctouStubRowIndexer : IRowIndexer
{
    private readonly IRowIndexer _inner;
    private readonly long _fakeTotalRows;
    private readonly int _incompleteCallCount;
    private int _isCompletedCalls;

    public ToctouStubRowIndexer(IRowIndexer inner, long fakeTotalRows, int incompleteCallCount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(incompleteCallCount, 1);
        _inner = inner;
        _fakeTotalRows = fakeTotalRows;
        _incompleteCallCount = incompleteCallCount;
    }

    public string FilePath => _inner.FilePath;
    public long FileSize => _inner.FileSize;
    public long BytesRead => _inner.BytesRead;
    public long TotalRows => _fakeTotalRows;
    public bool IsIndexingCompleted => Interlocked.Increment(ref _isCompletedCalls) > _incompleteCallCount;

    /// <summary>
    /// The number of times <see cref="IsIndexingCompleted"/> has been read. Exposed so tests can
    /// assert the production call-count contract explicitly instead of depending on it silently.
    /// </summary>
    internal int IsCompletedCallCount => Volatile.Read(ref _isCompletedCalls);

#pragma warning disable CS0067
    public event Action? FirstCheckpointReached;
#pragma warning restore CS0067
    public event Action<long, long>? ProgressChanged;
    public event Action? BuildIndexCompleted;

    public void RaiseProgressChanged(long bytesRead, long fileSize) =>
        ProgressChanged?.Invoke(bytesRead, fileSize);

    public void RaiseBuildIndexCompleted() =>
        BuildIndexCompleted?.Invoke();

    public void BuildIndex(CancellationToken ct = default) => _inner.BuildIndex(ct);

    public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) =>
        _inner.GetCheckPoint(targetRow);
}

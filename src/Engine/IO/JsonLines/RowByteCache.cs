namespace Refedle.Engine.IO.JsonLines;

/// <summary>
/// Manages a sliding window LRU cache of JSON line bytes for efficient virtual scrolling.
/// Uses ReadOnlyMemory&lt;byte&gt; for memory-efficient line storage.
/// </summary>
public sealed class RowByteCache(
    IRowIndexer indexer,
    int capacity = 200,
    int prefetchWindow = 20)
    : SlidingWindowLruCache<JsonRawBytes>(indexer, capacity, prefetchWindow), IDisposable
{
    private readonly RowReader _reader = new(indexer.FilePath);
    private bool _disposed;

    /// <inheritdoc/>
    protected override JsonRawBytes EmptyValue => JsonRawBytes.Empty;

    /// <inheritdoc/>
    public override JsonRawBytes GetRow(long index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return base.GetRow(index);
    }

    /// <inheritdoc/>
    protected override IEnumerable<JsonRawBytes> LoadRows(
        long byteOffset,
        int rowOffsetToSkip,
        int rowsToFetch) =>
        _reader.ReadLines(byteOffset, rowOffsetToSkip, rowsToFetch);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _reader.Dispose();
        _disposed = true;
    }
}

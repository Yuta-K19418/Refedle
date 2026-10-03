namespace Refedle.Engine.IO.JsonArray;

/// <summary>
/// Sliding window LRU cache for JSON Array element bytes.
/// Delegates byte loading to <see cref="ElementReader"/>.
/// </summary>
public sealed class ElementByteCache(
    IRowIndexer indexer,
    int capacity = 200,
    int prefetchWindow = 20)
    : SlidingWindowLruCache<JsonRawBytes>(indexer, capacity, prefetchWindow), IDisposable
{
    private readonly ElementReader _reader = new(indexer.FilePath);
    private bool _disposed;

    /// <inheritdoc/>
    public override JsonRawBytes GetRow(long index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return base.GetRow(index);
    }

    /// <inheritdoc/>
    protected override JsonRawBytes EmptyValue => JsonRawBytes.Empty;

    /// <inheritdoc/>
    protected override IEnumerable<JsonRawBytes> LoadRows(
        long byteOffset,
        int rowOffsetToSkip,
        int rowsToFetch) =>
        _reader.ReadElements(byteOffset, rowOffsetToSkip, rowsToFetch);

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

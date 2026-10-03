namespace Refedle.Engine.IO.Csv;

/// <summary>
/// Manages a sliding window LRU cache of CSV data rows for efficient virtual scrolling.
/// Uses ReadOnlyMemory for memory-efficient column storage.
/// </summary>
public sealed class DataRowCache(
    IRowIndexer indexer,
    int columnCount,
    int capacity = 200,
    int prefetchWindow = 20)
    : SlidingWindowLruCache<CsvDataRow>(indexer, capacity, prefetchWindow), IDisposable
{
    private readonly DataRowReader _reader = new(indexer.FilePath, columnCount);
    private bool _disposed;

    /// <inheritdoc/>
    protected override CsvDataRow EmptyValue => [];

    /// <inheritdoc/>
    public override CsvDataRow GetRow(long index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return base.GetRow(index);
    }

    /// <inheritdoc/>
    protected override IEnumerable<CsvDataRow> LoadRows(
        long byteOffset,
        int rowOffsetToSkip,
        int rowsToFetch) =>
        _reader.ReadRows(byteOffset, rowOffsetToSkip, rowsToFetch);

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

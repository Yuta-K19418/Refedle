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
    private bool _readFailed;

    /// <summary>
    /// Raised once, on the thread that called <see cref="GetRow"/>, when a window of rows
    /// cannot be read because a row does not have the header's column count. Afterwards the
    /// cache stops reading and returns empty rows.
    /// </summary>
    public event Action<string>? ReadFailed;

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
        ReadWindow(byteOffset, rowOffsetToSkip, rowsToFetch);

    private IReadOnlyList<CsvDataRow> ReadWindow(long byteOffset, int rowOffsetToSkip, int rowsToFetch)
    {
        if (_readFailed)
        {
            return [];
        }

        var result = _reader.ReadRows(byteOffset, rowOffsetToSkip, rowsToFetch);
        if (result.IsSuccess)
        {
            return result.Value;
        }

        _readFailed = true;
        ReadFailed?.Invoke(result.Error);
        return [];
    }

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

namespace Refedle.Engine.IO;

/// <summary>
/// Abstract base class implementing a sliding window LRU cache for row-based data.
/// Combines prefetch-based loading (centered on requested row) with LRU eviction
/// to optimize sequential and bidirectional scrolling scenarios.
/// </summary>
/// <typeparam name="TRow">The type of row data.</typeparam>
/// <remarks>
/// Thread Safety: This implementation is not thread-safe by design.
/// All access is assumed to occur on the TUI rendering thread (single-threaded context).
/// Future parallel access scenarios would require synchronization.
/// </remarks>
public abstract class SlidingWindowLruCache<TRow>
{
    private const int DefaultCapacity = 200;
    private const int DefaultPrefetchWindow = 20;

    private readonly Dictionary<long, LinkedListNode<CacheEntry<TRow>>> _cache;
    private readonly LinkedList<CacheEntry<TRow>> _lruList;
    private readonly int _capacity;
    private readonly int _prefetchWindow;
    private readonly IRowIndexer _indexer;

    /// <summary>
    /// Initializes the LRU cache with the given indexer, capacity, and prefetch window size.
    /// </summary>
    protected SlidingWindowLruCache(
        IRowIndexer indexer,
        int capacity = DefaultCapacity,
        int prefetchWindow = DefaultPrefetchWindow
    )
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(prefetchWindow);
        _indexer = indexer;
        _capacity = capacity;
        _prefetchWindow = prefetchWindow;
        _cache = new Dictionary<long, LinkedListNode<CacheEntry<TRow>>>(capacity);
        _lruList = new LinkedList<CacheEntry<TRow>>();
    }

    /// <summary>
    /// Gets the total number of rows in the data source.
    /// </summary>
    public long TotalRows => _indexer.TotalRows;

    /// <summary>
    /// Gets the row at the specified index.
    /// </summary>
    /// <param name="index">The zero-based row index.</param>
    /// <returns>The row data at the specified index, or empty value if out of range.</returns>
    public virtual TRow GetRow(long index)
    {
        if (index < 0 || index >= TotalRows)
        {
            return EmptyValue;
        }

        if (_cache.TryGetValue(index, out var node))
        {
            _lruList.MoveToFront(node);
            return node.ValueRef.Value;
        }

        Prefetch(index);
        return _cache.TryGetValue(index, out var fetchedNode)
            ? fetchedNode.ValueRef.Value
            : EmptyValue;
    }

    /// <summary>
    /// Loads a prefetch window centered around the requested row.
    /// The requested row is loaded first to prevent self-eviction.
    /// </summary>
    /// <param name="requestedRow">The row that was requested.</param>
    private void Prefetch(long requestedRow)
    {
        var halfWindow = _prefetchWindow / 2;
        var maxStart = TotalRows - _prefetchWindow;
        var windowStart = Math.Max(0, Math.Min(requestedRow - halfWindow, maxStart));
        var windowEnd = Math.Min(windowStart + _prefetchWindow - 1, TotalRows - 1);

        (var byteOffset, var rowOffsetToSkip) = _indexer.GetCheckPoint(windowStart);
        if (byteOffset < 0)
        {
            return;
        }

        // Window length never exceeds the int-sized prefetch window.
        var rows = LoadRows(byteOffset, rowOffsetToSkip, (int)(windowEnd - windowStart + 1));
        var rowsArray = rows.ToArray();

        var requestedIndex = requestedRow - windowStart;

        // Add non-requested rows first, then the requested row last.
        // This guarantees the requested row is the MRU entry after prefetch,
        // preventing self-eviction when prefetchWindow exceeds capacity.
        for (var i = 0; i < rowsArray.Length; i++)
        {
            if (i != requestedIndex)
            {
                AddOrUpdateCache(windowStart + i, rowsArray[i]);
            }
        }

        if (requestedIndex >= 0 && requestedIndex < rowsArray.Length)
        {
            AddOrUpdateCache(requestedRow, rowsArray[(int)requestedIndex]);
        }
    }

    private void AddOrUpdateCache(long rowIndex, TRow rowValue)
    {
        if (_cache.TryGetValue(rowIndex, out var node))
        {
            _lruList.MoveToFront(node);
            return;
        }

        if (_lruList.Count >= _capacity)
        {
            _lruList.ReuseTail(_cache, rowIndex, rowValue, EmptyValue);
            return;
        }

        _lruList.AddNew(_cache, rowIndex, rowValue);
    }

    /// <summary>
    /// Gets the empty/default value for the row type, used to reset cache entries.
    /// </summary>
    protected abstract TRow EmptyValue { get; }

    /// <summary>
    /// Loads a range of rows from the underlying data source.
    /// </summary>
    /// <param name="byteOffset">The byte offset within the file to start reading from.</param>
    /// <param name="rowOffsetToSkip">The number of rows to skip from the byte offset.</param>
    /// <param name="rowsToFetch">The number of rows to fetch.</param>
    /// <returns>An enumerable of row data.</returns>
    protected abstract IEnumerable<TRow> LoadRows(
        long byteOffset,
        int rowOffsetToSkip,
        int rowsToFetch
    );
}

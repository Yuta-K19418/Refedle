using AwesomeAssertions;
using Refedle.Engine.IO;

namespace Refedle.Tests.Engine.IO;

public sealed class SlidingWindowLruCacheTests
{
    private sealed class MockIndexer(long totalRows) : IRowIndexer
    {
#pragma warning disable CS0067
        public event Action? FirstCheckpointReached;
        public event Action<long, long>? ProgressChanged;
        public event Action? BuildIndexCompleted;
#pragma warning restore CS0067

        public string FilePath => "mock.csv";
        public long TotalRows => totalRows;
        public long BytesRead => 0;
        public long FileSize => totalRows * 10;
        public bool IsIndexingCompleted => true;

        public void BuildIndex(CancellationToken ct = default) { }

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow)
        {
            var byteOffset = targetRow * 10L;
            return (byteOffset, 0);
        }
    }

    private sealed class TestRowCache(
        MockIndexer indexer,
        int capacity = 10,
        int prefetchWindow = 4)
        : SlidingWindowLruCache<int>(indexer, capacity, prefetchWindow)
    {
        public List<(long ByteOffset, int RowOffset, int RowsToFetch)> LoadCalls = [];

        protected override int EmptyValue => -1;

        protected override IEnumerable<int> LoadRows(
            long byteOffset,
            int rowOffsetToSkip,
            int rowsToFetch)
        {
            LoadCalls.Add((byteOffset, rowOffsetToSkip, rowsToFetch));
            var startRow = (int)(byteOffset / 10);
            return Enumerable.Range(startRow, rowsToFetch);
        }
    }

    // Long-row variant to exercise row indices beyond int.MaxValue; the row value
    // is the row index itself, so it must be a long to represent such rows.
    private sealed class LongRowCache(
        MockIndexer indexer,
        int capacity = 10,
        int prefetchWindow = 4)
        : SlidingWindowLruCache<long>(indexer, capacity, prefetchWindow)
    {
        protected override long EmptyValue => -1L;

        protected override IEnumerable<long> LoadRows(
            long byteOffset,
            int rowOffsetToSkip,
            int rowsToFetch)
        {
            var startRow = byteOffset / 10 + rowOffsetToSkip;
            return Enumerable.Range(0, rowsToFetch).Select(i => startRow + i);
        }
    }

    [Fact]
    public void GetRow_OnCacheMiss_LoadsPrefetchWindow()
    {
        // Arrange
        var indexer = new MockIndexer(100);
        var cache = new TestRowCache(indexer, capacity: 10, prefetchWindow: 4);

        // Act
        var result = cache.GetRow(10);

        // Assert
        result.Should().Be(10);
        cache.LoadCalls.Should().HaveCount(1);
        var (byteOffset, rowOffset, rows) = cache.LoadCalls[0];
        byteOffset.Should().Be(80);
        rowOffset.Should().Be(0);
        rows.Should().Be(4);
    }

    [Fact]
    public void GetRow_OnCacheHit_UpdatesLruWithoutIo()
    {
        // Arrange
        var indexer = new MockIndexer(100);
        var cache = new TestRowCache(indexer, capacity: 10, prefetchWindow: 4);
        cache.GetRow(10);

        // Act
        var result = cache.GetRow(10);

        // Assert
        result.Should().Be(10);
        cache.LoadCalls.Should().HaveCount(1);
    }

    [Fact]
    public void GetRow_WindowAtFileStart_ClampsPrefetchWindowToStart()
    {
        // Arrange
        var indexer = new MockIndexer(100);
        var cache = new TestRowCache(indexer, capacity: 10, prefetchWindow: 4);

        // Act
        var result = cache.GetRow(0);

        // Assert
        result.Should().Be(0);
        var (byteOffset, rowOffset, rows) = cache.LoadCalls[0];
        byteOffset.Should().Be(0);
        rowOffset.Should().Be(0);
        rows.Should().Be(4);
    }

    [Fact]
    public void GetRow_WindowAtFileEnd_ClampsPrefetchWindowToEnd()
    {
        // Arrange
        var indexer = new MockIndexer(100);
        var cache = new TestRowCache(indexer, capacity: 10, prefetchWindow: 4);

        // Act
        var result = cache.GetRow(99);

        // Assert
        result.Should().Be(99);
        var (_, _, rows) = cache.LoadCalls[0];
        rows.Should().Be(4);
        cache.GetRow(96).Should().Be(96);
        cache.GetRow(99).Should().Be(99);
    }

    [Fact]
    public void GetRow_WhenCacheFull_EvictsLruTail()
    {
        // Arrange
        var indexer = new MockIndexer(100);
        var cache = new TestRowCache(indexer, capacity: 10, prefetchWindow: 2);
        WarmupCache(cache, Enumerable.Range(0, 5).Select(i => i * 2));

        // Act
        var result = cache.GetRow(10);

        // Assert
        result.Should().Be(10);
        cache.LoadCalls.Count.Should().Be(6);
        cache.GetRow(0);                            // reload occurs if row 0 was evicted
        cache.LoadCalls.Count.Should().Be(7);       // I/O increased = proof of eviction
    }

    [Fact]
    public void GetRow_AlreadyCachedRowInPrefetchWindow_UpdatesLruWithoutDuplicate()
    {
        // Arrange
        var indexer = new MockIndexer(100);
        var cache = new TestRowCache(indexer, capacity: 10, prefetchWindow: 4);
        cache.GetRow(10);

        // Act
        var result = cache.GetRow(11);

        // Assert
        result.Should().Be(11);
        cache.LoadCalls.Should().HaveCount(1);
    }

    [Fact]
    public void GetRow_FileSmallerThanCapacity_NoExcessAllocations()
    {
        // Arrange
        var indexer = new MockIndexer(10);
        var cache = new TestRowCache(indexer, capacity: 20, prefetchWindow: 4);

        // Act
        WarmupCache(cache, Enumerable.Range(0, 10));

        // Assert
        cache.LoadCalls.Count.Should().Be(4);
    }

    [Fact]
    public void GetRow_OutOfRangeIndex_ReturnsDefaultValue()
    {
        // Arrange
        var indexer = new MockIndexer(100);
        var cache = new TestRowCache(indexer);

        // Act
        var result = cache.GetRow(100);

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public void GetRow_WhenTotalRowsIsZero_ReturnsDefaultValue()
    {
        // Arrange
        var indexer = new MockIndexer(0);
        var cache = new TestRowCache(indexer);

        // Act
        var result = cache.GetRow(0);

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public void GetRow_CapacitySmallerThanPrefetchWindow_RequestedRowIsNotEvicted()
    {
        // Arrange
        var indexer = new MockIndexer(100);
        var cache = new TestRowCache(indexer, capacity: 2, prefetchWindow: 4);

        // Act
        var result = cache.GetRow(10);

        // Assert
        result.Should().Be(10);
        cache.GetRow(10).Should().Be(10);           // can be re-retrieved
        cache.LoadCalls.Should().HaveCount(1);      // I/O has not increased = proof that row was not evicted
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(long.MinValue)]
    public void GetRow_NegativeIndex_ReturnsDefaultValue(long negativeIndex)
    {
        // Arrange
        var indexer = new MockIndexer(100);
        var cache = new TestRowCache(indexer);

        // Act
        var result = cache.GetRow(negativeIndex);

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public void TotalRows_WhenCountExceedsIntMaxValue_ReturnsExactCount()
    {
        // Arrange
        var indexer = new MockIndexer(3_000_000_000);
        var cache = new LongRowCache(indexer);

        // Act
        var totalRows = cache.TotalRows;

        // Assert
        totalRows.Should().Be(3_000_000_000L);
    }

    [Fact]
    public void GetRow_IndexExceedingIntMaxValue_ReturnsCorrectRow()
    {
        // Arrange
        var indexer = new MockIndexer(3_000_000_000);
        var cache = new LongRowCache(indexer);

        // Act
        var result = cache.GetRow(2_500_000_000);

        // Assert
        result.Should().Be(2_500_000_000L);
    }

    [Fact]
    public void GetRow_LastRowExceedsIntMaxValue_ReturnsCorrectRow()
    {
        // Arrange
        var indexer = new MockIndexer(3_000_000_000);
        var cache = new LongRowCache(indexer);

        // Act
        var result = cache.GetRow(2_999_999_999);

        // Assert
        result.Should().Be(2_999_999_999L);
    }

    [Fact]
    public void GetRow_IndicesWithSameIntRepresentation_ReturnsDistinctCachedRows()
    {
        // Arrange - 4_294_967_297 (2^32 + 1) truncates to 1 as int; the two rows
        // must remain distinct LRU entries keyed by their full long index.
        var indexer = new MockIndexer(4_294_967_298);
        var cache = new LongRowCache(indexer, capacity: 10, prefetchWindow: 1);

        // Act
        var first = cache.GetRow(1);
        var second = cache.GetRow(4_294_967_297);
        var reloadedFirst = cache.GetRow(1);

        // Assert
        first.Should().Be(1L);
        second.Should().Be(4_294_967_297L);
        reloadedFirst.Should().Be(1L);
    }

    [Fact]
    public void Constructor_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        MockIndexer indexer = null!;

        // Act
        Action act = () => _ = new TestRowCache(indexer!, capacity: 10, prefetchWindow: 5);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithZeroCapacity_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var indexer = new MockIndexer(10);

        // Act
        Action act = () => _ = new TestRowCache(indexer, capacity: 0, prefetchWindow: 5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithZeroPrefetchWindow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var indexer = new MockIndexer(10);

        // Act
        Action act = () => _ = new TestRowCache(indexer, capacity: 10, prefetchWindow: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static void WarmupCache(TestRowCache cache, IEnumerable<int> rows)
    {
        foreach (var row in rows)
        {
            cache.GetRow(row);
        }
    }
}

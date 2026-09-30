using AwesomeAssertions;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed partial class RowByteCacheTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly RowIndexer _indexer;
    private bool _disposed;

    public RowByteCacheTests()
    {
        // Arrange
        _testFilePath = Path.GetTempFileName();

        // Create JSON Lines file for testing
        var lines = new[]
        {
            "{\"id\":1,\"name\":\"Alice\"}",
            "{\"id\":2,\"name\":\"Bob\"}",
            "{\"id\":3,\"name\":\"Charlie\"}",
            "{\"id\":4,\"name\":\"David\"}",
            "{\"id\":5,\"name\":\"Eve\"}",
            "{\"id\":6,\"name\":\"Frank\"}",
            "{\"id\":7,\"name\":\"Grace\"}",
            "{\"id\":8,\"name\":\"Henry\"}",
            "{\"id\":9,\"name\":\"Ivy\"}",
            "{\"id\":10,\"name\":\"Jack\"}",
        };

        File.WriteAllLines(_testFilePath, lines);

        // Create RowIndexer and build index - MmapService is not used
        _indexer = new RowIndexer(_testFilePath);
        _indexer.BuildIndex();
    }

    [Fact]
    public void GetRow_WithinCachedRange_ReturnsCachedBytes()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, capacity: 5, prefetchWindow: 20);
        var expectedLine2 = "{\"id\":2,\"name\":\"Bob\"}"u8.ToArray();

        // Act - Get line 2 (within cache)
        var result1 = cache.GetRow(1).ToArray();
        // Get again (cache hit)
        var result2 = cache.GetRow(1).ToArray();

        // Assert
        result1.Should().BeEquivalentTo(expectedLine2);
        result2.Should().BeEquivalentTo(expectedLine2);
    }

    [Fact]
    public void GetRow_OutsideCachedRange_UpdatesCacheWindow()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, capacity: 5, prefetchWindow: 20);
        var expectedLine1 = "{\"id\":1,\"name\":\"Alice\"}"u8.ToArray();
        var expectedLine8 = "{\"id\":8,\"name\":\"Henry\"}"u8.ToArray();

        // Act - First access caches lines 0-4
        var result1 = cache.GetRow(0).ToArray();
        // Next access to line 7 updates cache window
        var result2 = cache.GetRow(7).ToArray();

        // Assert
        result1.Should().BeEquivalentTo(expectedLine1);
        result2.Should().BeEquivalentTo(expectedLine8);
    }

    [Fact]
    public void GetRow_FirstLineRequested_CachesFromBeginning()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, capacity: 3, prefetchWindow: 20);
        var expectedLine1 = "{\"id\":1,\"name\":\"Alice\"}"u8.ToArray();
        var expectedLine2 = "{\"id\":2,\"name\":\"Bob\"}"u8.ToArray();
        var expectedLine3 = "{\"id\":3,\"name\":\"Charlie\"}"u8.ToArray();

        // Act
        var result1 = cache.GetRow(0).ToArray();
        var result2 = cache.GetRow(1).ToArray();
        var result3 = cache.GetRow(2).ToArray();

        // Assert
        result1.Should().BeEquivalentTo(expectedLine1);
        result2.Should().BeEquivalentTo(expectedLine2);
        result3.Should().BeEquivalentTo(expectedLine3);
    }

    [Fact]
    public void GetRow_LastLineRequested_CachesToEnd()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, capacity: 3, prefetchWindow: 20);
        var lastLineIndex = _indexer.TotalRows - 1;
        var expectedLastLine = "{\"id\":10,\"name\":\"Jack\"}"u8.ToArray();

        // Act
        var result = cache.GetRow(lastLineIndex).ToArray();

        // Assert
        result.Should().BeEquivalentTo(expectedLastLine);
    }

    [Fact]
    public void Constructor_WithEmptyFile_ThrowsInvalidOperationException()
    {
        // Arrange
        using var tempFile = new TempFile();
        File.WriteAllText(tempFile.Path, string.Empty);

        var indexer = new RowIndexer(tempFile.Path);
        indexer.BuildIndex();

        // Act
        var act = () =>
        {
            using var cache = new RowByteCache(indexer, capacity: 200, prefetchWindow: 20);
        };

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(long.MinValue)]
    public void GetRow_NegativeIndex_ReturnsEmpty(long invalidIndex)
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, capacity: 200, prefetchWindow: 20);

        // Act
        var result = cache.GetRow(invalidIndex);

        // Assert
        result.IsEmpty.Should().BeTrue();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    public void GetRow_IndexEqualToOrGreaterThanTotalLines_ReturnsEmpty(int overflowIndex)
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, capacity: 200, prefetchWindow: 20);

        // Act
        var result = cache.GetRow(overflowIndex);

        // Assert
        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void GetRow_RequestedAtCacheCenter_ReturnsSameValue()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, capacity: 5, prefetchWindow: 20);
        cache.GetRow(1); // prime the cache
        var beforeAccess = cache.GetRow(1).ToArray(); // baseline from cached value

        // Act
        var afterAccess = cache.GetRow(1).ToArray();

        // Assert
        afterAccess.Should().BeEquivalentTo(beforeAccess);
    }

    [Fact]
    public void GetRow_RequestedNearWindowEdge_ReturnsCorrectRows()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, capacity: 5, prefetchWindow: 20);
        var expectedLine4 = "{\"id\":4,\"name\":\"David\"}"u8.ToArray();
        var expectedLine8 = "{\"id\":8,\"name\":\"Henry\"}"u8.ToArray();

        // First access sets cache window (lines 0-4)
        cache.GetRow(0);

        // Act - Access edge of cache window (line 4)
        var result1 = cache.GetRow(3).ToArray();
        // Access line outside cache (line 8)
        var result2 = cache.GetRow(7).ToArray();

        // Assert
        result1.Should().BeEquivalentTo(expectedLine4);
        result2.Should().BeEquivalentTo(expectedLine8);
    }

    [Fact]
    public void GetRow_WithCacheLargerThanTotalLines_ReturnsAllLines()
    {
        // Arrange
        var largeCapacity = 20; // Larger than total lines
        using var cache = new RowByteCache(_indexer, capacity: largeCapacity, prefetchWindow: 20);

        // Act - Get first and last lines
        var firstLine = cache.GetRow(0).ToArray();
        var lastLine = cache.GetRow(9).ToArray();

        // Assert
        var expectedFirst = "{\"id\":1,\"name\":\"Alice\"}"u8.ToArray();
        var expectedLast = "{\"id\":10,\"name\":\"Jack\"}"u8.ToArray();

        firstLine.Should().BeEquivalentTo(expectedFirst);
        lastLine.Should().BeEquivalentTo(expectedLast);
    }

    [Fact]
    public void Dispose_AfterAccess_PreventsFurtherAccess()
    {
        // Arrange - row 0 is already in cache (cache-hit path) when Dispose is called;
        // verifies that the dispose guard fires before any cache lookup.
        using var cache = new RowByteCache(_indexer, capacity: 200, prefetchWindow: 20);
        cache.GetRow(0);

        // Act
        cache.Dispose();
        var act = () => cache.GetRow(0);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetRow_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange - cache is empty (cache-miss path) when GetRow is called after Dispose;
        // verifies that the dispose guard fires before any I/O attempt.
        using var cache = new RowByteCache(_indexer, capacity: 200, prefetchWindow: 20);
        cache.Dispose();

        // Act
        var act = () => cache.GetRow(0);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetRow_WithExactCacheSize_CachesExactly()
    {
        // Arrange
        var capacity = 5;
        using var cache = new RowByteCache(_indexer, capacity: capacity, prefetchWindow: 20);

        // First access initializes cache (lines 0-4)
        cache.GetRow(0);

        // Act - Get last line within cache range
        var result = cache.GetRow(4).ToArray();

        // Assert
        var expected = "{\"id\":5,\"name\":\"Eve\"}"u8.ToArray();
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GetRow_WithMultipleWindowShifts_ReturnsCorrectRows()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, capacity: 4, prefetchWindow: 20);
        cache.GetRow(0); // prime first window
        var firstCached = cache.GetRow(2).ToArray();

        // Act - shift window by accessing a row far outside the current cache
        cache.GetRow(8);
        var newCached = cache.GetRow(7).ToArray();

        // Assert
        var expectedOld = "{\"id\":3,\"name\":\"Charlie\"}"u8.ToArray();
        var expectedNew = "{\"id\":8,\"name\":\"Henry\"}"u8.ToArray();
        firstCached.Should().BeEquivalentTo(expectedOld);
        newCached.Should().BeEquivalentTo(expectedNew);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            File.Delete(_testFilePath);
            _disposed = true;
        }
    }
}

sealed file class TempFile : IDisposable
{
    private readonly string _path = System.IO.Path.GetTempFileName();
    public string Path => _path;
    public void Dispose() => System.IO.File.Delete(_path);
}

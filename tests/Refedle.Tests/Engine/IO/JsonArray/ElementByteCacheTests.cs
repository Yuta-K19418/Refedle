using System.Text;
using AwesomeAssertions;
using Refedle.Engine.IO.JsonArray;

namespace Refedle.Tests.Engine.IO.JsonArray;

public sealed class ElementByteCacheTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly RowIndexer _indexer;
    private bool _disposed;

    public ElementByteCacheTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonarray_elementbytecache_{Guid.NewGuid()}.json"
        );

        string[] elements =
        [
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
        ];

        File.WriteAllText(_testFilePath, $"[{string.Join(",", elements)}]");

        _indexer = new RowIndexer(_testFilePath);
        _indexer.BuildIndex();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }

        _disposed = true;
    }

    [Fact]
    public void GetRow_FirstAccess_ReturnsCorrectBytes()
    {
        // Arrange
        using var cache = new ElementByteCache(_indexer);

        // Act
        var result = cache.GetRow(0);

        // Assert
        Encoding.UTF8.GetString(result.Span).Should().Be("{\"id\":1,\"name\":\"Alice\"}");
    }

    [Fact]
    public void GetRow_SecondAccessSameIndex_ReturnsSameBytes()
    {
        // Arrange
        using var cache = new ElementByteCache(_indexer);

        // Act
        var first = cache.GetRow(0);
        var second = cache.GetRow(0);

        // Assert
        first.ToArray().Should().Equal(second.ToArray());
    }

    [Fact]
    public void GetRow_LastValidIndex_ReturnsCorrectBytes()
    {
        // Arrange
        using var cache = new ElementByteCache(_indexer);
        var lastIndex = cache.TotalRows - 1;

        // Act
        var result = cache.GetRow(lastIndex);

        // Assert
        Encoding.UTF8.GetString(result.Span).Should().Be("{\"id\":10,\"name\":\"Jack\"}");
    }

    [Fact]
    public void GetRow_OutsideCachedRange_UpdatesCacheWindow()
    {
        // Arrange
        using var cache = new ElementByteCache(_indexer, capacity: 5, prefetchWindow: 3);

        // Act — access index 0 to prime cache, then access index 9 (outside initial window)
        _ = cache.GetRow(0);
        var result = cache.GetRow(9);

        // Assert
        Encoding.UTF8.GetString(result.Span).Should().Be("{\"id\":10,\"name\":\"Jack\"}");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void GetRow_NegativeIndex_ReturnsEmpty(long invalidIndex)
    {
        // Arrange
        using var cache = new ElementByteCache(_indexer);

        // Act
        var result = cache.GetRow(invalidIndex);

        // Assert
        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void GetRow_IndexEqualToTotalElements_ReturnsEmpty()
    {
        // Arrange
        using var cache = new ElementByteCache(_indexer);
        var totalRows = cache.TotalRows;

        // Act
        var result = cache.GetRow(totalRows);

        // Assert
        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void GetRow_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var cache = new ElementByteCache(_indexer);
        cache.Dispose();

        // Act
        var act = () => cache.GetRow(0);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var cache = new ElementByteCache(_indexer);

        // Act
        cache.Dispose();
        var act = () => cache.Dispose();

        // Assert
        act.Should().NotThrow();
    }
}

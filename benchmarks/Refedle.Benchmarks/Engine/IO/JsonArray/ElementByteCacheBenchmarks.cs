using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO.JsonArray;

namespace Refedle.Benchmarks.Engine.IO.JsonArray;

/// <summary>
/// Benchmarks JSON array element caching.
/// </summary>
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
[MemoryDiagnoser]
public class ElementByteCacheBenchmarks : IDisposable
{
    private readonly string _tempFilePath1k;
    private readonly int _elementCount;
    private readonly RowIndexer _indexer;

    /// <summary>
    /// Creates benchmark data.
    /// </summary>
    public ElementByteCacheBenchmarks()
    {
        var id = Guid.NewGuid();
        _tempFilePath1k = Path.Combine(
            Path.GetTempPath(),
            $"jsonarray_cache_bench_1k_{id}.json"
        );

        _elementCount = 1000;
        var elements = Enumerable.Range(0, _elementCount)
            .Select(i => $"{{\"id\":{i},\"value\":\"item_{i}\"}}");
        File.WriteAllText(_tempFilePath1k, $"[{string.Join(",", elements)}]");

        _indexer = new RowIndexer(_tempFilePath1k);
        _indexer.BuildIndex();
    }

    /// <summary>
    /// Releases benchmark resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases benchmark resources.
    /// </summary>
    /// <param name="disposing">Indicates whether managed resources should be released.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        if (File.Exists(_tempFilePath1k))
        {
            File.Delete(_tempFilePath1k);
        }
    }

    /// <summary>
    /// Reads elements sequentially.
    /// </summary>
    [Benchmark]
    public void GetRow_SequentialAccess()
    {
        // Arrange — create a fresh cache for each benchmark run
        using var cache = new ElementByteCache(_indexer);
        JsonRawBytes result = default;

        // Act — sequential GetRow(0.._elementCount) calls
        for (var i = 0; i < _elementCount; i++)
        {
            result = cache.GetRow(i);
        }

        // Assert (prevent JIT elimination)
        _ = result.Length + _elementCount;
    }

    /// <summary>
    /// Reads elements in pseudo-random order.
    /// </summary>
    [Benchmark]
    public void GetRow_RandomAccess()
    {
        // Arrange — create a fresh cache for each benchmark run
        using var cache = new ElementByteCache(_indexer);
        JsonRawBytes result = default;

        // Act — random GetRow calls within 0.._elementCount using a simple LCG pattern
        var index = 0;
        for (var i = 0; i < _elementCount; i++)
        {
            index = (index * 1664525 + 1013904223) & 0x7FFFFFFF;
            result = cache.GetRow(index % _elementCount);
        }

        // Assert (prevent JIT elimination)
        _ = result.Length + _elementCount;
    }
}

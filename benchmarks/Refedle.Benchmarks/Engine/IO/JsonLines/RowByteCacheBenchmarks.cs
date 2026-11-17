using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Benchmarks.Engine.IO.JsonLines;

/// <summary>
/// Benchmarks JSON Lines row caching.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.NativeAot11_0)]
public class RowByteCacheBenchmarks : IDisposable
{
    private readonly string _testFilePath;
    private readonly RowIndexer _indexer;
    private readonly Random _random = new(42); // Fixed seed for reproducibility
    private bool _disposed;

    /// <summary>
    /// Gets or sets the cache capacity.
    /// </summary>
    [Params(100, 200, 500)]
    public int Capacity { get; set; }

    /// <summary>
    /// Creates benchmark data.
    /// </summary>
    public RowByteCacheBenchmarks()
    {
        // Arrange - Create test data
        _testFilePath = Path.Combine(Path.GetTempPath(), $"row_byte_cache_benchmark_{Guid.NewGuid()}.jsonl");

        // Create 10,000 lines of test data
        string[] lines = [.. Enumerable
            .Range(1, 10_000)
            .Select(i => $"{{\"id\":{i},\"name\":\"User{i}\",\"age\":{i % 100}}}")];

        File.WriteAllLines(_testFilePath, lines);

        // Initialize RowIndexer - MmapService is not used
        _indexer = new RowIndexer(_testFilePath);
        _indexer.BuildIndex();
    }

    /// <summary>
    /// Accesses random rows with an approximately fifty percent cache hit rate.
    /// </summary>
    [Benchmark]
    public void Access_RandomPattern_CacheHit50()
    {
        RowByteCache cache = new(_indexer, capacity: Capacity, prefetchWindow: 20);
        long totalLines = _indexer.TotalRows;

        // Random access pattern (approximately 50% cache hit rate)
        for (int i = 0; i < 1000; i++)
        {
            int lineIndex;
            if (i % 2 == 0)
            {
                // Random row within cache
                lineIndex = _random.Next(0, Math.Min(100, (int)totalLines));
            }
            else
            {
                // Random row outside cache
                lineIndex = _random.Next(Capacity + 50, (int)totalLines - 1);
            }

            JsonRawBytes bytes = cache.GetRow(lineIndex);
            _ = bytes.Length; // Use result (prevent optimization)
        }

        cache.Dispose();
    }

    /// <summary>
    /// Accesses sequential rows with an approximately ninety percent cache hit rate.
    /// </summary>
    [Benchmark]
    public void Access_SequentialPattern_CacheHit90()
    {
        RowByteCache cache = new(_indexer, capacity: Capacity, prefetchWindow: 20);

        // Sequential access pattern (approximately 90% cache hit rate)
        for (int i = 0; i < 1000; i++)
        {
            // Sequential access (within the same window)
            int lineIndex = i % Capacity;

            JsonRawBytes bytes = cache.GetRow(lineIndex);
            _ = bytes.Length; // Use result (prevent optimization)
        }

        cache.Dispose();
    }

    /// <summary>
    /// Repeatedly accesses one cached row.
    /// </summary>
    [Benchmark]
    public void Access_RepeatedSameLine_CacheHit100()
    {
        RowByteCache cache = new(_indexer, capacity: Capacity, prefetchWindow: 20);

        // Repeated access to the same line (100% cache hit rate)
        for (int i = 0; i < 1000; i++)
        {
            JsonRawBytes bytes = cache.GetRow(50);
            _ = bytes.Length; // Use result (prevent optimization)
        }

        cache.Dispose();
    }

    /// <summary>
    /// Accesses rows with the configured cache size.
    /// </summary>
    [Benchmark]
    public void Access_VaryingCacheSizes()
    {
        // Performance measurement with various cache sizes
        RowByteCache cache = new(_indexer, capacity: Capacity, prefetchWindow: 20);
        long totalLines = _indexer.TotalRows;

        // Mixed access pattern
        for (int i = 0; i < 500; i++)
        {
            // Random access
            int lineIndex = _random.Next(0, (int)totalLines - 1);
            JsonRawBytes bytes = cache.GetRow(lineIndex);
            _ = bytes.Length;
        }

        cache.Dispose();
    }

    /// <summary>
    /// Accesses a ten-thousand-line file.
    /// </summary>
    [Benchmark]
    public void Access_LargeFile_10kLines()
    {
        RowByteCache cache = new(_indexer, capacity: Capacity, prefetchWindow: 20);

        // Random access with large file
        for (int i = 0; i < 200; i++)
        {
            int lineIndex = _random.Next(0, 10_000);
            JsonRawBytes bytes = cache.GetRow(lineIndex);
            _ = bytes.Length;
        }

        cache.Dispose();
    }

    /// <summary>
    /// Accesses a one-hundred-thousand-line file.
    /// </summary>
    [Benchmark]
    public void Access_LargeFile_100kLines()
    {
        // Create separate file with 100k lines (for benchmarking only)
        string largeFilePath = Path.Combine(Path.GetTempPath(), $"row_byte_cache_benchmark_large_{Guid.NewGuid()}.jsonl");
        try
        {
            string[] lines = [.. Enumerable
                .Range(1, 100_000)
                .Select(i => $"{{\"id\":{i},\"data\":\"LargeDataset{i}\"}}")];

            File.WriteAllLines(largeFilePath, lines);

            // MmapService is not used
            RowIndexer indexer = new(largeFilePath);
            indexer.BuildIndex();

            using RowByteCache cache = new(indexer, capacity: Capacity, prefetchWindow: 20);

            // Random access with large file
            for (int i = 0; i < 100; i++)
            {
                int lineIndex = _random.Next(0, 100_000);
                JsonRawBytes bytes = cache.GetRow(lineIndex);
                _ = bytes.Length;
            }
        }
        finally
        {
            File.Delete(largeFilePath);
        }
    }

    /// <summary>
    /// Initializes a cache for the first time.
    /// </summary>
    [Benchmark]
    public void InitializeCache_FirstTime()
    {
        // Measure cache initialization cost
        RowByteCache cache = new(_indexer, capacity: Capacity, prefetchWindow: 20);

        // Initial access
        JsonRawBytes bytes = cache.GetRow(0);
        _ = bytes.Length;

        cache.Dispose();
    }

    /// <summary>
    /// Initializes a cache after disposal.
    /// </summary>
    [Benchmark]
    public void InitializeCache_AfterDisposal()
    {
        // Measure reinitialization cost after disposal
        RowByteCache cache1 = new(_indexer, capacity: Capacity, prefetchWindow: 20);
        cache1.Dispose();

        RowByteCache cache2 = new(_indexer, capacity: Capacity);
        JsonRawBytes bytes = cache2.GetRow(0);
        _ = bytes.Length;

        cache2.Dispose();
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
        if (!disposing || _disposed)
        {
            return;
        }

        File.Delete(_testFilePath);
        _disposed = true;
    }

}

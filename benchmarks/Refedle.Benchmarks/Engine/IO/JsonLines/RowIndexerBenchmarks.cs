using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Benchmarks.Engine.IO.JsonLines;

/// <summary>
/// Benchmarks JSON Lines row indexing.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class RowIndexerBenchmarks
{
    private readonly string _tempFilePath;

    /// <summary>
    /// Initializes the benchmark file path.
    /// </summary>
    public RowIndexerBenchmarks()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"jsonlines_benchmark_{Guid.NewGuid()}.jsonl");
    }

    /// <summary>
    /// Creates benchmark data.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // Create test data for benchmarks
        var lines = Enumerable.Range(0, 100_000)
            .Select(i => $"{{\"id\": {i}, \"name\": \"User{i}\", \"data\": \"This is a test string with various characters\"}}");
        File.WriteAllText(_tempFilePath, string.Join("\n", lines));
    }

    /// <summary>
    /// Deletes benchmark data.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    /// <summary>
    /// Builds the row index.
    /// </summary>
    [Benchmark]
    public void BuildIndex_100kRows()
    {
        var indexer = new RowIndexer(_tempFilePath);
        indexer.BuildIndex();
    }

    /// <summary>
    /// Gets the first checkpoint.
    /// </summary>
    [Benchmark]
    public void GetCheckPoint_FirstRow()
    {
        var indexer = new RowIndexer(_tempFilePath);
        indexer.BuildIndex();
        _ = indexer.GetCheckPoint(0);
    }

    /// <summary>
    /// Gets the middle checkpoint.
    /// </summary>
    [Benchmark]
    public void GetCheckPoint_MiddleRow()
    {
        var indexer = new RowIndexer(_tempFilePath);
        indexer.BuildIndex();
        _ = indexer.GetCheckPoint(50_000);
    }

    /// <summary>
    /// Gets the final checkpoint.
    /// </summary>
    [Benchmark]
    public void GetCheckPoint_LastRow()
    {
        var indexer = new RowIndexer(_tempFilePath);
        indexer.BuildIndex();
        _ = indexer.GetCheckPoint(99_999);
    }

    /// <summary>
    /// Gets multiple checkpoints.
    /// </summary>
    [Benchmark]
    public void GetCheckPoint_MultipleCalls()
    {
        var indexer = new RowIndexer(_tempFilePath);
        indexer.BuildIndex();

        // Simulate multiple lookups
        _ = indexer.GetCheckPoint(0);
        _ = indexer.GetCheckPoint(25_000);
        _ = indexer.GetCheckPoint(50_000);
        _ = indexer.GetCheckPoint(75_000);
        _ = indexer.GetCheckPoint(99_999);
    }
}

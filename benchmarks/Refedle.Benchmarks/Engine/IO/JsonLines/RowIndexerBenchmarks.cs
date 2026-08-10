using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Benchmarks.Engine.IO.JsonLines;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class RowIndexerBenchmarks
{
    private readonly string _tempFilePath;

    public RowIndexerBenchmarks()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"jsonlines_benchmark_{Guid.NewGuid()}.jsonl");
    }

    [GlobalSetup]
    public void Setup()
    {
        // Create test data for benchmarks
        var lines = Enumerable.Range(0, 100_000)
            .Select(i => $"{{\"id\": {i}, \"name\": \"User{i}\", \"data\": \"This is a test string with various characters\"}}");
        File.WriteAllText(_tempFilePath, string.Join("\n", lines));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    [Benchmark]
    public void BuildIndex_100kRows()
    {
        var indexer = new RowIndexer(_tempFilePath);
        indexer.BuildIndex();
    }

    [Benchmark]
    public void GetCheckPoint_FirstRow()
    {
        var indexer = new RowIndexer(_tempFilePath);
        indexer.BuildIndex();
        _ = indexer.GetCheckPoint(0);
    }

    [Benchmark]
    public void GetCheckPoint_MiddleRow()
    {
        var indexer = new RowIndexer(_tempFilePath);
        indexer.BuildIndex();
        _ = indexer.GetCheckPoint(50_000);
    }

    [Benchmark]
    public void GetCheckPoint_LastRow()
    {
        var indexer = new RowIndexer(_tempFilePath);
        indexer.BuildIndex();
        _ = indexer.GetCheckPoint(99_999);
    }

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

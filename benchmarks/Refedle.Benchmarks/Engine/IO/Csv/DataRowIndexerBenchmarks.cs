using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO.Csv;

namespace Refedle.Benchmarks.Engine.IO.Csv;

[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Median", "RatioSD")]
public class DataRowIndexerBenchmarks : IDisposable
{
    private readonly string _smallFilePath;
    private readonly string _mediumFilePath;
    private readonly string _largeFilePath;
    private readonly string _complexFilePath;

    public DataRowIndexerBenchmarks()
    {
        // Small file: 1,000 rows (~50 KB)
        _smallFilePath = Path.Combine(Path.GetTempPath(), $"bench_csv_small_{Guid.NewGuid()}.csv");
        var smallLines = new List<string> { "id,name,email,age,city" };
        smallLines.AddRange(
            Enumerable
                .Range(0, 1_000)
                .Select(i => $"{i},User{i:D6},user{i}@example.com,{20 + i % 50},City{i % 100}")
        );
        File.WriteAllLines(_smallFilePath, smallLines);

        // Medium file: 100,000 rows (~5 MB)
        _mediumFilePath = Path.Combine(
            Path.GetTempPath(),
            $"bench_csv_medium_{Guid.NewGuid()}.csv"
        );
        var mediumLines = new List<string> { "id,name,email,age,city" };
        mediumLines.AddRange(
            Enumerable
                .Range(0, 100_000)
                .Select(i => $"{i},User{i:D6},user{i}@example.com,{20 + i % 50},City{i % 100}")
        );
        File.WriteAllLines(_mediumFilePath, mediumLines);

        // Large file: 1,000,000 rows (~50 MB)
        _largeFilePath = Path.Combine(Path.GetTempPath(), $"bench_csv_large_{Guid.NewGuid()}.csv");
        var largeLines = new List<string> { "id,name,email,age,city" };
        largeLines.AddRange(
            Enumerable
                .Range(0, 1_000_000)
                .Select(i => $"{i},User{i:D6},user{i}@example.com,{20 + i % 50},City{i % 100}")
        );
        File.WriteAllLines(_largeFilePath, largeLines);

        // Complex file with quoted fields: 10,000 rows
        _complexFilePath = Path.Combine(
            Path.GetTempPath(),
            $"bench_csv_complex_{Guid.NewGuid()}.csv"
        );
        var complexLines = new List<string> { "id,name,address,notes" };
        complexLines.AddRange(
            Enumerable
                .Range(0, 10_000)
                .Select(i =>
                    i % 3 == 0
                        ? $"{i},\"Smith, John\",\"123 Main St\nApt {i}\",\"Has a pet\""
                        : $"{i},User{i},\"Address {i}\",Normal"
                )
        );
        File.WriteAllLines(_complexFilePath, complexLines);
    }

    [Benchmark(Description = "Small CSV (1K rows, ~50KB)")]
    public void IndexSmallCsv()
    {
        var indexer = new DataRowIndexer(_smallFilePath);
        indexer.BuildIndex();
    }

    [Benchmark(Description = "Medium CSV (100K rows, ~5MB)")]
    public void IndexMediumCsv()
    {
        var indexer = new DataRowIndexer(_mediumFilePath);
        indexer.BuildIndex();
    }

    [Benchmark(Description = "Large CSV (1M rows, ~50MB)")]
    public void IndexLargeCsv()
    {
        var indexer = new DataRowIndexer(_largeFilePath);
        indexer.BuildIndex();
    }

    [Benchmark(Description = "Complex CSV with quoted fields (10K rows)")]
    public void IndexComplexCsvWithQuotedFields()
    {
        var indexer = new DataRowIndexer(_complexFilePath);
        indexer.BuildIndex();
    }

    [Benchmark(Description = "GetCheckPoint for row 50,000")]
    public void GetCheckpointMediumFile()
    {
        var indexer = new DataRowIndexer(_mediumFilePath);
        indexer.BuildIndex();
        _ = indexer.GetCheckPoint(50_000);
    }

    [Benchmark(Description = "GetCheckPoint for row 500,000")]
    public void GetCheckpointLargeFile()
    {
        var indexer = new DataRowIndexer(_largeFilePath);
        indexer.BuildIndex();
        _ = indexer.GetCheckPoint(500_000);
    }

    public void Dispose()
    {
        if (File.Exists(_smallFilePath))
        {
            File.Delete(_smallFilePath);
        }

        if (File.Exists(_mediumFilePath))
        {
            File.Delete(_mediumFilePath);
        }

        if (File.Exists(_largeFilePath))
        {
            File.Delete(_largeFilePath);
        }

        if (File.Exists(_complexFilePath))
        {
            File.Delete(_complexFilePath);
        }

        GC.SuppressFinalize(this);
    }
}

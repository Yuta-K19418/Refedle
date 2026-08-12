using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO.JsonArray;

namespace Refedle.Benchmarks.Engine.IO.JsonArray;

/// <summary>
/// Benchmarks JSON array row indexing.
/// </summary>
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
[MemoryDiagnoser]
public class RowIndexerBenchmarks : IDisposable
{
    private const int ElementCount1k = 1_000;
    private const int ElementCount100k = 100_000;
    private const int ElementCount1m = 1_000_000;

    private readonly string _tempFilePath1k;
    private readonly string _tempFilePath100k;
    private readonly string _tempFilePath1m;
    private readonly RowIndexer _prebuiltIndexer;

    /// <summary>
    /// Creates benchmark data.
    /// </summary>
    public RowIndexerBenchmarks()
    {
        var id = Guid.NewGuid();
        _tempFilePath1k = Path.Combine(
            Path.GetTempPath(),
            $"jsonarray_bench_1k_{id}.json"
        );
        _tempFilePath100k = Path.Combine(
            Path.GetTempPath(),
            $"jsonarray_bench_100k_{id}.json"
        );
        _tempFilePath1m = Path.Combine(
            Path.GetTempPath(),
            $"jsonarray_bench_1m_{id}.json"
        );

        WriteElements(_tempFilePath1k, ElementCount1k);
        WriteElements(_tempFilePath100k, ElementCount100k);
        WriteElements(_tempFilePath1m, ElementCount1m);

        _prebuiltIndexer = new RowIndexer(_tempFilePath1m);
        _prebuiltIndexer.BuildIndex();
    }

    /// <summary>
    /// Indexes one thousand elements.
    /// </summary>
    [Benchmark]
    public void BuildIndex_1kElements()
    {
        var indexer = new RowIndexer(_tempFilePath1k);
        indexer.BuildIndex();
    }

    /// <summary>
    /// Indexes one hundred thousand elements.
    /// </summary>
    [Benchmark]
    public void BuildIndex_100kElements()
    {
        var indexer = new RowIndexer(_tempFilePath100k);
        indexer.BuildIndex();
    }

    /// <summary>
    /// Indexes one million elements.
    /// </summary>
    [Benchmark]
    public void BuildIndex_1mElements()
    {
        var indexer = new RowIndexer(_tempFilePath1m);
        indexer.BuildIndex();
    }

    /// <summary>
    /// Gets the first checkpoint.
    /// </summary>
    [Benchmark]
    public void GetCheckPoint_First()
    {
        _prebuiltIndexer.GetCheckPoint(0);
    }

    /// <summary>
    /// Gets the middle checkpoint.
    /// </summary>
    [Benchmark]
    public void GetCheckPoint_Middle()
    {
        _prebuiltIndexer.GetCheckPoint(ElementCount1m / 2);
    }

    /// <summary>
    /// Gets the final checkpoint.
    /// </summary>
    [Benchmark]
    public void GetCheckPoint_Last()
    {
        _prebuiltIndexer.GetCheckPoint(ElementCount1m - 1);
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

        File.Delete(_tempFilePath1k);
        File.Delete(_tempFilePath100k);
        File.Delete(_tempFilePath1m);
    }

    private static void WriteElements(string path, int count)
    {
        using var writer = new StreamWriter(path);
        writer.Write('[');
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            writer.Write($"{{\"id\":{i}}}");
        }

        writer.Write(']');
    }
}

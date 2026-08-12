using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO.JsonObject;

namespace Refedle.Benchmarks.Engine.IO.JsonObject;

/// <summary>
/// Benchmarks top-level JSON object scanning.
/// </summary>
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
[MemoryDiagnoser]
public class TopLevelScannerBenchmarks
{
    private const int KeyCount10 = 10;
    private const int KeyCount100 = 100;

    private string _tempFilePath10Keys = string.Empty;
    private string _tempFilePath100KeysSmall = string.Empty;
    private string _tempFilePath100KeysLarge = string.Empty;

    /// <summary>
    /// Creates benchmark data.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        var id = Guid.NewGuid();
        _tempFilePath10Keys = Path.Combine(
            Path.GetTempPath(),
            $"jsonobject_bench_10_{id}.json"
        );
        _tempFilePath100KeysSmall = Path.Combine(
            Path.GetTempPath(),
            $"jsonobject_bench_100small_{id}.json"
        );
        _tempFilePath100KeysLarge = Path.Combine(
            Path.GetTempPath(),
            $"jsonobject_bench_100large_{id}.json"
        );

        WriteKeyValues(_tempFilePath10Keys, KeyCount10, static i => $"{i}");
        WriteKeyValues(_tempFilePath100KeysSmall, KeyCount100, static i => $"{i}");
        WriteKeyValues(_tempFilePath100KeysLarge, KeyCount100, static i => GenerateNestedObject(i));
    }

    /// <summary>
    /// Deletes benchmark data.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (_tempFilePath10Keys.Length > 0)
        {
            File.Delete(_tempFilePath10Keys);
        }

        if (_tempFilePath100KeysSmall.Length > 0)
        {
            File.Delete(_tempFilePath100KeysSmall);
        }

        if (_tempFilePath100KeysLarge.Length > 0)
        {
            File.Delete(_tempFilePath100KeysLarge);
        }
    }

    /// <summary>
    /// Scans ten keys with small values.
    /// </summary>
    [Benchmark]
    public void Scan_10Keys_SmallValues()
    {
        TopLevelScanner.Scan(_tempFilePath10Keys);
    }

    /// <summary>
    /// Scans one hundred keys with small values.
    /// </summary>
    [Benchmark]
    public void Scan_100Keys_SmallValues()
    {
        TopLevelScanner.Scan(_tempFilePath100KeysSmall);
    }

    /// <summary>
    /// Scans one hundred keys with large nested values.
    /// </summary>
    [Benchmark]
    public void Scan_100Keys_LargeNestedValues()
    {
        TopLevelScanner.Scan(_tempFilePath100KeysLarge);
    }

    private static void WriteKeyValues(string path, int count, Func<int, string> valueFactory)
    {
        using var writer = new StreamWriter(path);
        writer.Write('{');
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            writer.Write($"\"key{i}\":{valueFactory(i)}");
        }

        writer.Write('}');
    }

    private static string GenerateNestedObject(int seed)
    {
        var fields = string.Join(",", Enumerable.Range(0, 1000).Select(j => $"\"f{j}\":{seed + j}"));
        return $"{{{fields}}}";
    }
}

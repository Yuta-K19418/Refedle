using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.App.Tui.Workers.Schema.Csv;
using Refedle.Engine.Models;

namespace Refedle.Benchmarks.App.Tui.Workers.Schema.Csv;

/// <summary>
/// Benchmarks the background schema scan of the CSV incremental schema scanner, including file I/O.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class IncrementalSchemaScannerBenchmarks : IDisposable
{
    private readonly string _tempFilePath = Path.Combine(
        Path.GetTempPath(),
        $"incremental_schema_scan_benchmark_{Guid.NewGuid()}.csv");

    private IncrementalSchemaScanner _scanner = null!;
    private TableSchema _initialSchema = null!;

    /// <summary>
    /// Gets or sets the number of data rows in the input file.
    /// </summary>
    [Params(25_000, 50_000, 100_000)]
    public int RowCount { get; set; }

    /// <summary>
    /// Creates the input file and runs the initial scan outside the measured region.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        using (var writer = new StreamWriter(_tempFilePath))
        {
            writer.WriteLine("id,name,email,age,city");
            for (var i = 0; i < RowCount; i++)
            {
                writer.WriteLine($"{i},User{i},user{i}@example.com,{20 + (i % 50)},City{i % 100}");
            }
        }

        _scanner = new IncrementalSchemaScanner(_tempFilePath);
        _initialSchema = _scanner.InitialScanAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs the background schema scan to completion.
    /// </summary>
    /// <returns>The refined schema.</returns>
    [Benchmark(Description = "Background scan of RowCount rows")]
    public TableSchema ScanBackground()
    {
        return _scanner.StartBackgroundScanAsync(_initialSchema, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Deletes the temporary input file.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Deletes the temporary input file.
    /// </summary>
    /// <param name="disposing">Whether managed resources are being released.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }
}

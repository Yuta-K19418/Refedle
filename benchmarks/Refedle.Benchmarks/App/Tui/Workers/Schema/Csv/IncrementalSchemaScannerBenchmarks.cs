using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.App.Tui.Workers.Schema;
using Refedle.App.Tui.Workers.Schema.Csv;

namespace Refedle.Benchmarks.App.Tui.Workers.Schema.Csv;

/// <summary>
/// Benchmarks the background schema scan of the CSV incremental schema scanner, including file I/O.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class IncrementalSchemaScannerBenchmarks : IncrementalSchemaScannerBenchmarksBase
{
    /// <inheritdoc/>
    protected override string FileExtension => ".csv";

    /// <inheritdoc/>
    private protected override void WriteRows(StreamWriter writer, int rowCount)
    {
        writer.WriteLine("id,name,email,age,city");
        for (var i = 0; i < rowCount; i++)
        {
            writer.WriteLine($"{i},User{i},user{i}@example.com,{20 + (i % 50)},City{i % 100}");
        }
    }

    private protected override ISchemaScanner CreateScanner(string filePath)
    {
        return new IncrementalSchemaScanner(filePath);
    }
}

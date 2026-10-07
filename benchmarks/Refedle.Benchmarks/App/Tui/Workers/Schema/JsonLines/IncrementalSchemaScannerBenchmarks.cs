using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.App.Tui.Workers.Schema;
using Refedle.App.Tui.Workers.Schema.JsonLines;

namespace Refedle.Benchmarks.App.Tui.Workers.Schema.JsonLines;

/// <summary>
/// Benchmarks the background schema scan of the JSON Lines incremental schema scanner, including file I/O.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class IncrementalSchemaScannerBenchmarks : IncrementalSchemaScannerBenchmarksBase
{
    /// <inheritdoc/>
    protected override string FileExtension => ".jsonl";

    /// <inheritdoc/>
    private protected override void WriteRows(StreamWriter writer, int rowCount)
    {
        for (var i = 0; i < rowCount; i++)
        {
            writer.WriteLine($"{{\"id\":{i},\"name\":\"User{i}\",\"email\":\"user{i}@example.com\",\"age\":{20 + (i % 50)},\"city\":\"City{i % 100}\"}}");
        }
    }

    private protected override ISchemaScanner CreateScanner(string filePath)
    {
        return new IncrementalSchemaScanner(filePath);
    }
}

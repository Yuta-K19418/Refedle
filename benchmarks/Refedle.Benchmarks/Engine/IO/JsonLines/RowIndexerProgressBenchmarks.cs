using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Benchmarks.Engine.IO.JsonLines;

/// <summary>
/// Benchmarks JSON Lines row indexing with a progress subscriber that queues work per notification.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class RowIndexerProgressBenchmarks : RowIndexerProgressBenchmarksBase
{
    /// <inheritdoc/>
    protected override string FileExtension => ".jsonl";

    /// <inheritdoc/>
    private protected override void WriteRows(StreamWriter writer, int rowCount)
    {
        for (var i = 0; i < rowCount; i++)
        {
            writer.Write($"{{\"id\":{i},\"name\":\"User{i}\",\"email\":\"user{i}@example.com\",\"city\":\"City{i % 100}\",\"note\":\"short padding text\"}}\n");
        }
    }

    private protected override IRowIndexer CreateIndexer(string filePath)
    {
        return new RowIndexer(filePath);
    }
}

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO;
using Refedle.Engine.IO.Csv;

namespace Refedle.Benchmarks.Engine.IO.Csv;

/// <summary>
/// Benchmarks CSV row indexing with a progress subscriber that queues work per notification.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class DataRowIndexerProgressBenchmarks : RowIndexerProgressBenchmarksBase
{
    /// <inheritdoc/>
    protected override string FileExtension => ".csv";

    /// <inheritdoc/>
    private protected override void WriteRows(StreamWriter writer, int rowCount)
    {
        writer.Write("id,name,email,age,city,note\n");
        for (var i = 0; i < rowCount; i++)
        {
            writer.Write($"{i},User{i},user{i}@example.com,{20 + (i % 50)},City{i % 100},short padding text for row width\n");
        }
    }

    private protected override IRowIndexer CreateIndexer(string filePath)
    {
        return new DataRowIndexer(filePath);
    }
}

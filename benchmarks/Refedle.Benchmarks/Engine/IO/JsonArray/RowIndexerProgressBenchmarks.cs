using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO;
using Refedle.Engine.IO.JsonArray;

namespace Refedle.Benchmarks.Engine.IO.JsonArray;

/// <summary>
/// Benchmarks JSON array row indexing with a progress subscriber that queues work per notification.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class RowIndexerProgressBenchmarks : RowIndexerProgressBenchmarksBase
{
    /// <inheritdoc/>
    protected override string FileExtension => ".json";

    /// <inheritdoc/>
    private protected override void WriteRows(StreamWriter writer, int rowCount)
    {
        writer.Write('[');
        for (var i = 0; i < rowCount; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            writer.Write($"{{\"id\":{i},\"name\":\"User{i}\",\"email\":\"user{i}@example.com\",\"city\":\"City{i % 100}\",\"note\":\"short padding text\"}}");
        }

        writer.Write(']');
    }

    private protected override IRowIndexer CreateIndexer(string filePath)
    {
        return new RowIndexer(filePath);
    }
}

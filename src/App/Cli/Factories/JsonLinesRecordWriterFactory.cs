using Refedle.Engine;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.Factories;

[RecordWriter(DataFormat.JsonLines)]
internal readonly struct JsonLinesRecordWriterFactory : IRecordWriterFactory<JsonLinesRecordWriter>
{
    private const int StreamBufferSize = 1024 * 64; // 64 KB
    public ValueTask<JsonLinesRecordWriter> CreateAsync(
        string outputFile,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct)
    {
        FileStream stream = new(outputFile, FileMode.Create, FileAccess.Write, FileShare.Read, StreamBufferSize, useAsync: true);
        return new(new JsonLinesRecordWriter(stream, outputSchema));
    }
}

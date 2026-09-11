using Refedle.Engine;

namespace Refedle.App.Cli.IO.Factories;

internal interface IRecordWriterFactory<TWriter> where TWriter : struct, IRecordWriter
{
    ValueTask<TWriter> CreateAsync(
        string outputFile,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct);
}

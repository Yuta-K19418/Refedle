using Refedle.Engine;

namespace Refedle.App.Cli.Factories;

internal interface IRecordReaderFactory<TReader> where TReader : struct, IRecordReader
{
    ValueTask<TReader> CreateAsync(Arguments args, IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct);
}

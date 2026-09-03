using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.App.Cli.Factories;

internal interface IRecordReaderFactory<TReader> where TReader : struct, IRecordReader
{
    ValueTask<TReader> CreateAsync(
        string inputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
        IReadOnlyList<string> inputColumnNames,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct);
}

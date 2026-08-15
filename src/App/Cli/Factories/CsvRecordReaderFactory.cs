using nietras.SeparatedValues;
using Refedle.Engine;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.Factories;

[RecordReader(DataFormat.Csv)]
internal readonly struct CsvRecordReaderFactory : IRecordReaderFactory<CsvRecordReader>
{
    public async ValueTask<CsvRecordReader> CreateAsync(Arguments args, IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
    {
        var sepReader = await Sep.New(',').Reader().FromFileAsync(args.InputFile, ct).ConfigureAwait(false);
        return new CsvRecordReader(sepReader, outputSchema);
    }
}

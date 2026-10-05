using Refedle.App.Cli.IO.Csv;
using Refedle.Engine;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.IO.Factories;

[RecordReader(DataFormat.Csv)]
internal readonly struct CsvRecordReaderFactory : IRecordReaderFactory<CsvRecordReader>
{
    public async ValueTask<CsvRecordReader> CreateAsync(
        string inputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
        IReadOnlyList<string> inputColumnNames,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct)
    {
        var sepReader = await CsvSep.FromFileAsync(inputFile, cancellationToken: ct).ConfigureAwait(false);
        return new CsvRecordReader(sepReader, outputSchema);
    }
}

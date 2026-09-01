using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.Factories;

[RecordReader(DataFormat.JsonLines)]
internal readonly struct JsonLinesRecordReaderFactory : IRecordReaderFactory<JsonLinesRecordReader>
{
    public ValueTask<JsonLinesRecordReader> CreateAsync(
        string inputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
        IReadOnlyList<string> inputColumnNames,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct)
    {
        RowIndexer rowIndexer = new(inputFile);
        rowIndexer.BuildIndex(CancellationToken.None);

        // Zero-record input: RowReader must not be constructed (MmapService rejects empty
        // files); a reader without one yields no rows.
        if (rowIndexer.TotalRows == 0)
        {
            return new(new JsonLinesRecordReader(rowIndexer, null, inputColumnNames, outputSchema));
        }

        RowReader rowReader = new(inputFile);
        return new(new JsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema));
    }
}

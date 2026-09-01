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
        // Runner rejects zero-byte input before dispatch, so RowIndexer.TotalRows is
        // always > 0 here and RowReader (which MmapService cannot open on an empty file)
        // is always constructible.
        RowIndexer rowIndexer = new(inputFile);
        rowIndexer.BuildIndex(CancellationToken.None);

        RowReader rowReader = new(inputFile);
        return new(new JsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema));
    }
}

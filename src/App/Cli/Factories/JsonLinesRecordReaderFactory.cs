using Refedle.Engine;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.Factories;

[RecordReader(DataFormat.JsonLines)]
internal readonly struct JsonLinesRecordReaderFactory : IRecordReaderFactory<JsonLinesRecordReader>
{
    public ValueTask<JsonLinesRecordReader> CreateAsync(Arguments args, IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct)
    {
        Engine.IO.JsonLines.RowIndexer rowIndexer = new(args.InputFile);
        rowIndexer.BuildIndex(CancellationToken.None);

        // Zero-record input: RowReader must not be constructed (MmapService rejects empty
        // files); a reader without one yields no rows (see design_cli_batch_column_resolution.md).
        if (rowIndexer.TotalRows == 0)
        {
            return new(new JsonLinesRecordReader(rowIndexer, null, inputColumnNames, outputSchema));
        }

        Engine.IO.JsonLines.RowReader rowReader = new(args.InputFile);
        return new(new JsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema));
    }
}

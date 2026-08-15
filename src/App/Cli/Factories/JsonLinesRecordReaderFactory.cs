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
        Engine.IO.JsonLines.RowReader rowReader = new(args.InputFile);
        return new(new JsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema));
    }
}

using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.Factories;

/// <summary>
/// Creates the JSON Lines record reader, selecting the bare or Full Aggregation DrillDown arm
/// of <see cref="JsonLinesRecordReader"/> (ADR-6) from whether the recipe carries a KeyPath.
/// </summary>
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
        rowIndexer.BuildIndex(ct);

        if (drillDownKeyPath is null)
        {
            // Runner rejects zero-byte input before dispatch, so RowReader (which MmapService
            // cannot open on an empty file) is always constructible on either arm.
            RowReader bareRowReader = new(inputFile);
            return new(new JsonLinesRecordReader(
                new BareJsonLinesRecordReader(rowIndexer, bareRowReader, inputColumnNames, outputSchema)));
        }

        return new(new JsonLinesRecordReader(
            new FullAggregationRecordReader<JsonLinesBatchSourceReader>(
                rowIndexer, new JsonLinesBatchSourceReader(new RowReader(inputFile)), drillDownKeyPath, inputColumnNames, outputSchema)));
    }
}

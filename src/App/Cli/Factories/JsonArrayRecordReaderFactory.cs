using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonArray;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.Factories;

/// <summary>
/// Creates the Full Aggregation DrillDown record reader for JSON Array input: builds the
/// element index, wraps an <see cref="ElementReader"/> in <see cref="JsonArrayBatchSourceReader"/>,
/// and streams through <see cref="FullAggregationRecordReader{TBatchSourceReader}"/> (ADR-3).
/// A null KeyPath throws <see cref="InvalidOperationException"/>: DrillDownRecipeValidator and
/// ColumnNameResolver have already validated the same input before dispatch, so reaching this
/// factory with one is an upstream-contract violation. The Runner's catch-all reports the
/// message with exit code 1.
/// </summary>
[RecordReader(DataFormat.JsonArray)]
internal readonly struct JsonArrayRecordReaderFactory
    : IRecordReaderFactory<FullAggregationRecordReader<JsonArrayBatchSourceReader>>
{
    public ValueTask<FullAggregationRecordReader<JsonArrayBatchSourceReader>> CreateAsync(
        string inputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
        IReadOnlyList<string> inputColumnNames,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct)
    {
        if (drillDownKeyPath is null)
        {
            throw new InvalidOperationException(
                "DrillDownRecipeValidator and ColumnNameResolver already validated drillDownKeyPath for JsonArray input.");
        }

        // A zero-element array ("[]") still memory-maps, so ElementReader is always constructible.
        RowIndexer rowIndexer = new(inputFile);
        rowIndexer.BuildIndex(ct);

        var elementReader = new ElementReader(inputFile);
        return new(new FullAggregationRecordReader<JsonArrayBatchSourceReader>(
            rowIndexer, new JsonArrayBatchSourceReader(elementReader), drillDownKeyPath, inputColumnNames, outputSchema));
    }
}

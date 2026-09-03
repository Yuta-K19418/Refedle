using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Types;

namespace Refedle.App.Cli.Factories;

/// <summary>
/// Creates the Single DrillDown record reader for JSON Object input. All failure modes
/// throw <see cref="InvalidOperationException"/>: DrillDownRecipeValidator and
/// ColumnNameResolver have already validated and resolved the same input before dispatch,
/// so reaching this factory with a null/empty KeyPath or an unresolvable path is an
/// upstream-contract violation. The Runner's catch-all reports the message with exit code 1.
/// </summary>
[RecordReader(DataFormat.JsonObject)]
internal readonly struct JsonObjectRecordReaderFactory : IRecordReaderFactory<JsonObjectRecordReader>
{
    public async ValueTask<JsonObjectRecordReader> CreateAsync(
        string inputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
        IReadOnlyList<string> inputColumnNames,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct)
    {
        if (drillDownKeyPath is not { Count: > 0 })
        {
            throw new InvalidOperationException(
                "DrillDownRecipeValidator and ColumnNameResolver already validated drillDownKeyPath for JsonObject input.");
        }

        var fileBytes = await File.ReadAllBytesAsync(inputFile, ct).ConfigureAwait(false);
        var nodeResult = KeyPathNodeResolver.ResolveSingleNode(fileBytes, drillDownKeyPath);
        if (nodeResult.IsFailure)
        {
            throw new InvalidOperationException(nodeResult.Error);
        }

        var extractResult = DrillDownSchemaExtractor.ExtractFromNode(nodeResult.Value, DataFormat.JsonObject);
        if (extractResult.IsFailure)
        {
            throw new InvalidOperationException(extractResult.Error);
        }

        return new JsonObjectRecordReader(extractResult.Value.childRawValues, inputColumnNames, outputSchema);
    }
}

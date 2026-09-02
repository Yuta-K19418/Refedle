using Refedle.Engine;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Types;

namespace Refedle.App.Cli;

internal static class ColumnNameResolver
{
    private const int BatchSize = 1000;

    public static async Task<Result<IReadOnlyList<string>>> ResolveColumnNamesAsync(
        DataFormat inputFormat,
        string inputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
        CancellationToken ct)
    {
        return inputFormat switch
        {
            DataFormat.Csv => Results.Success(ColumnNameScanner.ScanColumnNames(inputFile)),
            DataFormat.JsonLines => drillDownKeyPath is null
                ? Results.Success<IReadOnlyList<string>>(ResolveJsonLinesColumnNames(inputFile, ct))
                : ResolveFullAggregationColumnNames(inputFile, inputFormat, drillDownKeyPath, ct),
            DataFormat.JsonObject => await ResolveJsonObjectColumnNamesAsync(inputFile, drillDownKeyPath, ct).ConfigureAwait(false),
            DataFormat.JsonArray => drillDownKeyPath is null
                ? throw new InvalidOperationException(
                    "DrillDownRecipeValidator already rejects null drillDownKeyPath for JsonArray input.")
                : ResolveFullAggregationColumnNames(inputFile, inputFormat, drillDownKeyPath, ct),
            _ => throw new NotSupportedException($"Unsupported format: {inputFormat}"),
        };
    }

    // Single DrillDown: resolve the recorded KeyPath to its node, then infer the column set
    // from that node's child objects (the same KeyPathNodeResolver + DrillDownSchemaExtractor
    // pair the TUI replays recipes with).
    private static async Task<Result<IReadOnlyList<string>>> ResolveJsonObjectColumnNamesAsync(
        string inputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
        CancellationToken ct)
    {
        if (drillDownKeyPath is not { Count: > 0 })
        {
            // Same message as the TUI's RecipeCommandHandler.LoadSingleDrillDownRecipe.
            return Results.Failure<IReadOnlyList<string>>(
                "This recipe's DrillDown path is empty, which is not valid for a JSON Object file.");
        }

        var fileBytes = await File.ReadAllBytesAsync(inputFile, ct).ConfigureAwait(false);
        var nodeResult = KeyPathNodeResolver.ResolveSingleNode(fileBytes, drillDownKeyPath);
        if (nodeResult.IsFailure)
        {
            return Results.Failure<IReadOnlyList<string>>(nodeResult.Error);
        }

        var extractResult = DrillDownSchemaExtractor.ExtractFromNode(nodeResult.Value, DataFormat.JsonObject);
        if (extractResult.IsFailure)
        {
            return Results.Failure<IReadOnlyList<string>>(extractResult.Error);
        }

        return Results.Success<IReadOnlyList<string>>(
            [.. extractResult.Value.schema.Columns.Select(c => c.Name)]);
    }

    // Full Aggregation DrillDown (JSON Array and JSON Lines): streams
    // the file through the same batch primitives the record reader uses, folding the schema
    // only — rows are discarded per record. An empty KeyPath is a valid scope (the whole
    // record is the leaf); a null KeyPath is rejected upstream, hence the non-nullable param.
    private static Result<IReadOnlyList<string>> ResolveFullAggregationColumnNames(
        string inputFile,
        DataFormat format,
        IReadOnlyList<KeyPathSegment> drillDownKeyPath,
        CancellationToken ct)
    {
        var scanResult = FullAggregationSchemaScanner.Scan(inputFile, format, drillDownKeyPath, ct);
        if (scanResult.IsFailure)
        {
            return Results.Failure<IReadOnlyList<string>>(scanResult.Error);
        }

        return Results.Success<IReadOnlyList<string>>(
            [.. scanResult.Value.Columns.Select(c => c.Name)]);
    }

    private static List<string> ResolveJsonLinesColumnNames(string inputFile, CancellationToken ct)
    {
        var rowIndexer = new RowIndexer(inputFile);
        rowIndexer.BuildIndex(ct);

        using var rowReader = new RowReader(inputFile);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<string>();
        var lineIndex = 0L;

        while (lineIndex < rowIndexer.TotalRows)
        {
            ct.ThrowIfCancellationRequested();

            var (byteOffset, rowOffset) = rowIndexer.GetCheckPoint(lineIndex);
            var lines = rowReader.ReadLines(byteOffset, rowOffset, BatchSize);
            if (lines.Count == 0)
            {
                break;
            }

            PropertyNameScanner.ScanPropertyNames(lines, seen, order);
            lineIndex += lines.Count;
        }

        return order;
    }
}

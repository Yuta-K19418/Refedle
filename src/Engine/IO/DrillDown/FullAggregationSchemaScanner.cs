using System.Diagnostics;
using System.Globalization;
using System.Text;
using Refedle.Engine.IO.JsonArray;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Engine.IO.DrillDown;

/// <summary>
/// Streaming Full Aggregation DrillDown schema scan: traverses a KeyPath for every record via
/// the batch-oriented RowIndexer/ElementReader primitives and folds schema accumulators only —
/// extracted rows are counted and discarded per batch, never retained (unlike
/// <see cref="FullAggregationScanner"/>, which materializes every row; ADR-3).
/// </summary>
public static class FullAggregationSchemaScanner
{
    private const int BatchSize = 1000;

    /// <summary>
    /// Scans <paramref name="filePath"/> for all records and traverses <paramref name="keyPath"/>
    /// to fold the union <see cref="TableSchema"/> of every matching leaf row. Returns
    /// <c>Failure</c> when no rows are collected or all collected leaf objects have no keys
    /// (the same conditions <see cref="FullAggregationScanner"/> reports).
    /// </summary>
    /// <remarks>
    /// Full Aggregation applies to JSON Array and JSON Lines input; <see cref="DataFormat.JsonObject"/>
    /// and any other value are unreachable.
    /// </remarks>
    public static Result<TableSchema> Scan(
        string filePath,
        DataFormat format,
        IReadOnlyList<KeyPathSegment> keyPath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(keyPath);

        return format switch
        {
            DataFormat.JsonArray => ScanJsonArray(filePath, format, keyPath, ct),
            DataFormat.JsonLines => ScanJsonLines(filePath, format, keyPath, ct),
            _ => throw new UnreachableException($"Full aggregation schema scan does not handle format '{format}'."),
        };
    }

    private static Result<TableSchema> ScanJsonArray(
        string filePath,
        DataFormat format,
        IReadOnlyList<KeyPathSegment> keyPath,
        CancellationToken ct)
    {
        var rowIndexer = new RowIndexer(filePath);
        rowIndexer.BuildIndex(ct);

        using var elementReader = new ElementReader(filePath);
        return ScanBatches(rowIndexer, elementReader.ReadElements, format, keyPath, ct);
    }

    private static Result<TableSchema> ScanJsonLines(
        string filePath,
        DataFormat format,
        IReadOnlyList<KeyPathSegment> keyPath,
        CancellationToken ct)
    {
        var rowIndexer = new JsonLines.RowIndexer(filePath);
        rowIndexer.BuildIndex(ct);

        using var rowReader = new JsonLines.RowReader(filePath);
        return ScanBatches(rowIndexer, rowReader.ReadLines, format, keyPath, ct);
    }

    // The shared accumulation loop: reads bounded batches through the format's batch reader,
    // applies KeyPathTraverser per record into scratch rows, and keeps only the schema
    // accumulators plus the total row count (needed for nullability accounting).
    private static Result<TableSchema> ScanBatches(
        RowIndexerBase rowIndexer,
        Func<long, int, int, IReadOnlyList<JsonRawBytes>> readBatch,
        DataFormat format,
        IReadOnlyList<KeyPathSegment> keyPath,
        CancellationToken ct)
    {
        var colName = KeyPathTraverser.LastKeySegment(keyPath);
        var colNameUtf8 = Encoding.UTF8.GetBytes(colName);

        List<FocusedTableRow> scratchRows = [];
        List<string> keyOrder = [];
        var keySet = new HashSet<string>(StringComparer.Ordinal);
        var columnTypes = new Dictionary<string, ColumnType>(StringComparer.Ordinal);
        var keyObservedCount = new Dictionary<string, int>(StringComparer.Ordinal);

        var totalRows = 0;
        var batchStart = 0L;
        while (batchStart < rowIndexer.TotalRows)
        {
            ct.ThrowIfCancellationRequested();

            var (byteOffset, rowOffset) = rowIndexer.GetCheckPoint(batchStart);
            var recordsToRead = (int)Math.Min(BatchSize, rowIndexer.TotalRows - batchStart);
            var batch = readBatch(byteOffset, rowOffset, recordsToRead);

            scratchRows.Clear();
            for (var i = 0; i < batch.Count; i++)
            {
                KeyPathTraverser.ExtractRows(
                    batch[i], keyPath, (i + 1).ToString(CultureInfo.InvariantCulture),
                    colName, colNameUtf8, scratchRows, keyOrder, keySet, columnTypes, keyObservedCount);
            }

            totalRows += scratchRows.Count;
            batchStart += recordsToRead;
        }

        if (totalRows == 0)
        {
            return Results.Failure<TableSchema>("No matching records found.");
        }

        if (keyOrder.Count == 0)
        {
            return Results.Failure<TableSchema>("All child objects have no keys");
        }

        return Results.Success(SchemaScanner.BuildTableSchema(keyOrder, columnTypes, keyObservedCount, totalRows, format));
    }
}

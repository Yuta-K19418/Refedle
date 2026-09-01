using Refedle.Engine.IO.Csv;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Types;

namespace Refedle.App.Cli;

internal static class ColumnNameResolver
{
    private const int BatchSize = 1000;

    public static IReadOnlyList<string> ResolveColumnNames(
        DataFormat inputFormat,
        string inputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
        CancellationToken ct) =>
        inputFormat switch
        {
            DataFormat.Csv => ColumnNameScanner.ScanColumnNames(inputFile),
            DataFormat.JsonLines => ResolveJsonLinesColumnNames(inputFile, ct),
            _ => throw new NotSupportedException($"Unsupported format: {inputFormat}"),
        };

    private static List<string> ResolveJsonLinesColumnNames(string inputFile, CancellationToken ct)
    {
        var rowIndexer = new RowIndexer(inputFile);
        rowIndexer.BuildIndex(ct);

        // Zero-record input resolves to an empty column list. RowReader must not be
        // constructed here: MmapService.Open rejects zero-byte files.
        if (rowIndexer.TotalRows == 0)
        {
            return [];
        }

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

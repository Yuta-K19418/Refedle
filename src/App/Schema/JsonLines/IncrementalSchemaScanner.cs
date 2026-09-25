using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;

namespace Refedle.App.Schema.JsonLines;

/// <summary>
/// Performs incremental schema inference for JSON Lines files.
/// - Initial scan: first 200 lines
/// - Background scan: remaining lines in batches of 1000
/// - Thread-safe schema updates via Copy-on-Write
/// </summary>
internal sealed class IncrementalSchemaScanner : IncrementalSchemaScannerBase
{
    /// <summary>
    /// Initializes a new instance of <see cref="IncrementalSchemaScanner"/>.
    /// </summary>
    /// <param name="filePath">Path to the JSON Lines file.</param>
    public IncrementalSchemaScanner(string filePath)
        : base(filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
    }

    /// <inheritdoc/>
    protected override TableSchema ExecuteInitialScan()
    {
        using var reader = new RowReader(FilePath);
        var lines = reader.ReadLines(
            byteOffset: 0,
            linesToSkip: 0,
            linesToRead: InitialScanCount
        );
        var scanResult = SchemaScanner.ScanSchema(lines, InitialScanCount);

        if (scanResult.IsFailure)
        {
            throw new InvalidOperationException(scanResult.Error);
        }

        return scanResult.Value;
    }

    /// <inheritdoc/>
    protected override TableSchema ExecuteBackgroundScan(
        TableSchema currentSchema,
        CancellationToken cancellationToken)
    {
        var lineIndex = InitialScanCount;
        var refinedSchema = currentSchema;

        while (!cancellationToken.IsCancellationRequested)
        {
            using var reader = new RowReader(FilePath);
            var lines = reader.ReadLines(
                byteOffset: 0,
                linesToSkip: lineIndex,
                linesToRead: BackgroundBatchSize
            );

            if (lines.Count == 0)
            {
                break;
            }

            foreach (var line in lines)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var refineResult = SchemaScanner.RefineSchema(refinedSchema, line.Span);
                if (refineResult.IsSuccess)
                {
                    refinedSchema = refineResult.Value;
                }
            }

            lineIndex += lines.Count;
        }

        return refinedSchema;
    }
}

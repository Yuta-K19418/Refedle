using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;

namespace Refedle.App.Tui.Workers.Schema.JsonLines;

/// <summary>
/// Performs incremental schema inference for JSON Lines files.
/// - Initial scan: first 200 lines
/// - Background scan: remaining lines after the initial scan, read in a single pass
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
        return BackgroundSchemaScan.Execute<JsonRawBytes>(
            currentSchema,
            ReadRemainingLines,
            static (schema, line) => SchemaScanner.RefineSchema(schema, line.Span),
            cancellationToken
        );
    }

    private IEnumerable<JsonRawBytes> ReadRemainingLines()
    {
        using var reader = new RowReader(FilePath);
        foreach (var line in reader.EnumerateLines(linesToSkip: InitialScanCount))
        {
            yield return line;
        }
    }
}

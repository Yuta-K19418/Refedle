using System.Globalization;
using nietras.SeparatedValues;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models;

namespace Refedle.App.Tui.Workers.Schema.Csv;

/// <summary>
/// Performs incremental schema inference for CSV files.
/// - Initial scan: first 200 rows
/// - Background scan: remaining rows after the initial scan, read in a single pass
/// - Thread-safe schema updates via Copy-on-Write
/// </summary>
internal sealed class IncrementalSchemaScanner : IncrementalSchemaScannerBase
{
    /// <summary>
    /// Creates a new incremental schema scanner.
    /// </summary>
    /// <param name="filePath">Path to the CSV file.</param>
    public IncrementalSchemaScanner(string filePath)
        : base(filePath)
    {
    }

    /// <inheritdoc/>
    protected override TableSchema ExecuteInitialScan()
    {
        var rows = ReadRows(0, InitialScanCount);
        var columnNames = ReadColumnNames();
        var scanResult = SchemaScanner.ScanSchema(columnNames, rows, InitialScanCount);

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
        return BackgroundSchemaScan.Execute<CsvDataRow>(
            currentSchema,
            ReadRemainingRows,
            SchemaScanner.RefineSchema,
            cancellationToken
        );
    }

    private IEnumerable<CsvDataRow> ReadRemainingRows()
    {
        using var reader = CsvSep.FromFile(FilePath);

        // Skip the rows consumed by the initial scan
        long skippedRows = 0;
        while (skippedRows < InitialScanCount && reader.MoveNext())
        {
            skippedRows++;
        }

        if (skippedRows < InitialScanCount)
        {
            yield break;
        }

        while (reader.MoveNext())
        {
            yield return CopyColumns(reader.Current);
        }
    }

    private List<CsvDataRow> ReadRows(int startRow, int count)
    {
        var rows = new List<CsvDataRow>(count);

        using var reader = CsvSep.FromFile(FilePath);

        // Skip to start row (0-based, where 0 means first data row after header)
        var currentRow = 0;
        while (currentRow < startRow && reader.MoveNext())
        {
            currentRow++;
        }

        // Read requested rows
        var readCount = 0;
        while (readCount < count && reader.MoveNext())
        {
            var columns = CopyColumns(reader.Current);

            rows.Add(columns);
            readCount++;
        }

        return rows;
    }

    private static ReadOnlyMemory<char>[] CopyColumns(SepReader.Row record)
    {
        var columns = new ReadOnlyMemory<char>[record.ColCount];

        for (var i = 0; i < record.ColCount; i++)
        {
            var columnSpan = record[i].Span;
            if (columnSpan.Length > 0)
            {
                // Copy span to memory (necessary for CsvDataRow which expects ReadOnlyMemory<char>)
                columns[i] = columnSpan.ToArray().AsMemory();
                continue;
            }

            columns[i] = ReadOnlyMemory<char>.Empty;
        }

        return columns;
    }

    private string[] ReadColumnNames()
    {
        using var reader = CsvSep.FromFile(FilePath);

        // Header column names are automatically available
        var header = reader.Header;
        if (header.ColNames.Count == 0)
        {
            throw new InvalidOperationException("CSV file has no columns.");
        }

        // Process column names
        var processedNames = new string[header.ColNames.Count];
        for (var i = 0; i < header.ColNames.Count; i++)
        {
            var columnName = header.ColNames[i];
            if (string.IsNullOrWhiteSpace(columnName))
            {
                columnName = string.Create(CultureInfo.InvariantCulture, $"Column{i + 1}");
            }

            processedNames[i] = columnName;
        }

        return processedNames;
    }
}

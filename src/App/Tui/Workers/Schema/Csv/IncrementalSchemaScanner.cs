using System.Globalization;
using nietras.SeparatedValues;
using Refedle.Engine;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models;

namespace Refedle.App.Tui.Workers.Schema.Csv;

/// <summary>
/// Performs incremental schema inference for CSV files.
/// - Initial scan: first 200 rows
/// - Background scan: remaining rows after the initial scan, read in a single pass
/// - A row whose column count differs from the header fails the initial scan and ends the
///   background scan, which keeps the schema inferred from the rows before it
/// - Thread-safe schema updates via Copy-on-Write
/// </summary>
/// <param name="filePath">Path to the CSV file.</param>
internal sealed class IncrementalSchemaScanner(string filePath) : IncrementalSchemaScannerBase(filePath)
{
    /// <inheritdoc/>
    protected override TableSchema ExecuteInitialScan()
    {
        string[] columnNames = ReadColumnNames();
        List<CsvDataRow> rows = ReadInitialRows(columnNames.Length);
        Result<TableSchema> scanResult = SchemaScanner.ScanSchema(columnNames, rows, InitialScanCount);

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
            () => ReadRemainingRows(currentSchema.ColumnCount),
            SchemaScanner.RefineSchema,
            cancellationToken
        );
    }

    private List<CsvDataRow> ReadInitialRows(int columnCount)
    {
        using DataRowReader reader = new(FilePath, columnCount);

        return [.. reader.EnumerateRows(rowsToSkip: 0).Take(InitialScanCount)];
    }

    private IEnumerable<CsvDataRow> ReadRemainingRows(int columnCount)
    {
        using DataRowReader reader = new(FilePath, columnCount);

        using IEnumerator<CsvDataRow> rows = reader.EnumerateRows(rowsToSkip: InitialScanCount).GetEnumerator();
        while (TryMoveNext(rows))
        {
            yield return rows.Current;
        }
    }

    // Only a column-count mismatch ends the scan; I/O errors still propagate
    private static bool TryMoveNext(IEnumerator<CsvDataRow> rows)
    {
        try
        {
            return rows.MoveNext();
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private string[] ReadColumnNames()
    {
        using SepReader reader = CsvSep.FromFile(FilePath);

        // Header column names are automatically available
        SepReaderHeader header = reader.Header;
        if (header.ColNames.Count == 0)
        {
            throw new InvalidOperationException("CSV file has no columns.");
        }

        // Process column names
        string[] processedNames = new string[header.ColNames.Count];
        for (int i = 0; i < header.ColNames.Count; i++)
        {
            string columnName = header.ColNames[i];
            if (string.IsNullOrWhiteSpace(columnName))
            {
                columnName = string.Create(CultureInfo.InvariantCulture, $"Column{i + 1}");
            }

            processedNames[i] = columnName;
        }

        return processedNames;
    }
}

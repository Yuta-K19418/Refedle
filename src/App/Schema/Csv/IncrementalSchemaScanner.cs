using System.Globalization;
using nietras.SeparatedValues;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models;

namespace Refedle.App.Schema.Csv;

/// <summary>
/// Performs incremental schema inference for CSV files.
/// - Initial scan: first 200 rows
/// - Background scan: remaining rows in batches of 1000
/// - Thread-safe schema updates via Copy-on-Write
/// </summary>
internal sealed class IncrementalSchemaScanner : IncrementalSchemaScannerBase
{
    private readonly string _filePath;

    /// <summary>
    /// Creates a new incremental schema scanner.
    /// </summary>
    /// <param name="filePath">Path to the CSV file.</param>
    public IncrementalSchemaScanner(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
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
        var rowIndex = InitialScanCount;
        var refinedSchema = currentSchema;

        while (!cancellationToken.IsCancellationRequested)
        {
            var rows = ReadRows(rowIndex, BackgroundBatchSize);
            if (rows.Count == 0)
            {
                break;
            }

            foreach (var row in rows)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var refineResult = SchemaScanner.RefineSchema(refinedSchema, row);
                if (refineResult.IsSuccess)
                {
                    refinedSchema = refineResult.Value;
                }
            }

            rowIndex += rows.Count;
        }

        return refinedSchema;
    }

    private List<CsvDataRow> ReadRows(int startRow, int count)
    {
        var rows = new List<CsvDataRow>(count);

        using var reader = Sep.New(',').Reader().FromFile(_filePath);

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
            var record = reader.Current;
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

            rows.Add(columns);
            readCount++;
        }

        return rows;
    }

    private string[] ReadColumnNames()
    {
        using var reader = Sep.New(',').Reader().FromFile(_filePath);

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

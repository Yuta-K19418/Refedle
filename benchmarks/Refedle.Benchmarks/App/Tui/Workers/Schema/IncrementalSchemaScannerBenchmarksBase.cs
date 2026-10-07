using BenchmarkDotNet.Attributes;
using Refedle.App.Tui.Workers.Schema;
using Refedle.Engine.Models;

namespace Refedle.Benchmarks.App.Tui.Workers.Schema;

/// <summary>
/// Shared benchmark body for the background schema scan of an incremental schema scanner, including file I/O.
/// Derived classes supply only the format-specific file contents and scanner.
/// </summary>
public abstract class IncrementalSchemaScannerBenchmarksBase : IDisposable
{
    private string _tempFilePath = string.Empty;
    private ISchemaScanner _scanner = null!;
    private TableSchema _initialSchema = null!;

    /// <summary>
    /// Gets or sets the number of data rows in the input file.
    /// </summary>
    [Params(25_000, 50_000, 100_000)]
    public int RowCount { get; set; }

    /// <summary>
    /// Gets the extension of the input file, including the leading dot.
    /// </summary>
    protected abstract string FileExtension { get; }

    /// <summary>
    /// Creates the input file and runs the initial scan outside the measured region.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _tempFilePath = Path.Combine(
            Path.GetTempPath(),
            $"incremental_schema_scan_benchmark_{Guid.NewGuid()}{FileExtension}");

        using (var writer = new StreamWriter(_tempFilePath))
        {
            WriteRows(writer, RowCount);
        }

        _scanner = CreateScanner(_tempFilePath);
        _initialSchema = _scanner.InitialScanAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs the background schema scan to completion.
    /// </summary>
    /// <returns>The refined schema.</returns>
    [Benchmark(Description = "Background scan of RowCount rows")]
    public TableSchema ScanBackground()
    {
        return _scanner.StartBackgroundScanAsync(_initialSchema, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Deletes the temporary input file.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Writes the whole input file, including any header, for the given number of data rows.
    /// </summary>
    /// <param name="writer">The writer over the input file.</param>
    /// <param name="rowCount">The number of data rows to write.</param>
    private protected abstract void WriteRows(StreamWriter writer, int rowCount);

    /// <summary>
    /// Creates the scanner under measurement for the input file.
    /// </summary>
    /// <param name="filePath">Path to the input file.</param>
    /// <returns>The scanner.</returns>
    private protected abstract ISchemaScanner CreateScanner(string filePath);

    /// <summary>
    /// Deletes the temporary input file.
    /// </summary>
    /// <param name="disposing">Whether managed resources are being released.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }
}

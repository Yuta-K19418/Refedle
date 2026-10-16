using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Refedle.Engine.IO;

namespace Refedle.Benchmarks.Engine.IO;

/// <summary>
/// Shared benchmark body for building a row index while a UI-like subscriber handles every progress notification.
/// Derived classes supply only the format-specific file contents and indexer.
/// </summary>
public abstract class RowIndexerProgressBenchmarksBase : IDisposable
{
    private string _tempFilePath = string.Empty;

    /// <summary>
    /// Gets or sets the number of data rows in the input file.
    /// </summary>
    [Params(100_000, 1_000_000)]
    public int RowCount { get; set; }

    /// <summary>
    /// Gets the extension of the input file, including the leading dot.
    /// </summary>
    protected abstract string FileExtension { get; }

    /// <summary>
    /// Creates the input file outside the measured region.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _tempFilePath = Path.Combine(
            Path.GetTempPath(),
            $"row_indexer_progress_benchmark_{Guid.NewGuid()}{FileExtension}");

        using var writer = new StreamWriter(_tempFilePath);
        WriteRows(writer, RowCount);
    }

    /// <summary>
    /// Builds the row index, queueing one closure per progress notification.
    /// </summary>
    /// <returns>The number of progress notifications raised.</returns>
    [Benchmark(Description = "BuildIndex with progress subscriber")]
    public int BuildIndexWithProgressSubscriber()
    {
        var indexer = CreateIndexer(_tempFilePath);
        var pendingActions = new ConcurrentQueue<Action>();
        var notificationCount = 0;

        indexer.ProgressChanged += (bytesRead, fileSize) =>
        {
            notificationCount++;
            pendingActions.Enqueue(() => _ = bytesRead + fileSize);
        };

        indexer.BuildIndex();

        return notificationCount;
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
    /// Each row is roughly 100 bytes.
    /// </summary>
    /// <param name="writer">The writer over the input file.</param>
    /// <param name="rowCount">The number of data rows to write.</param>
    private protected abstract void WriteRows(StreamWriter writer, int rowCount);

    /// <summary>
    /// Creates the indexer under measurement for the input file.
    /// </summary>
    /// <param name="filePath">Path to the input file.</param>
    /// <returns>The indexer.</returns>
    private protected abstract IRowIndexer CreateIndexer(string filePath);

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

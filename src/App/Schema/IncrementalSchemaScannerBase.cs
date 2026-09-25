using Refedle.Engine.Models;

namespace Refedle.App.Schema;

/// <summary>
/// Base class for incremental schema scanners that perform initial scan and background refinement.
/// - Initial scan: reads initial rows/lines in a single pass; awaited by the caller before displaying the table
/// - Background scan: continues refinement on remaining data in batches
/// - Thread-safe schema updates via Copy-on-Write pattern
/// </summary>
internal abstract class IncrementalSchemaScannerBase(string filePath) : ISchemaScanner
{
    protected const int InitialScanCount = 200;
    protected const int BackgroundBatchSize = 1000;

    /// <summary>
    /// Gets the path of the file being scanned.
    /// </summary>
    protected string FilePath { get; } = filePath ?? throw new ArgumentNullException(nameof(filePath));

    /// <summary>
    /// Performs initial scan to provide the first schema for UI display.
    /// Must complete before UI can render the initial table view.
    /// </summary>
    /// <returns>The initial table schema.</returns>
    public Task<TableSchema> InitialScanAsync()
    {
        return Task.Run(ExecuteInitialScan);
    }

    /// <summary>
    /// Starts background scan to refine schema with remaining data.
    /// Fire-and-forget operation - returns Task but should not be awaited.
    /// </summary>
    /// <param name="currentSchema">The schema produced by <see cref="InitialScanAsync"/>.</param>
    /// <param name="cancellationToken">Token to stop the background scan.</param>
    /// <returns>Task that completes with the final refined schema.</returns>
    public Task<TableSchema> StartBackgroundScanAsync(
        TableSchema currentSchema,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () =>
            {
                try
                {
                    return ExecuteBackgroundScan(currentSchema, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return currentSchema;
                }
            },
            cancellationToken
        );
    }

    /// <summary>
    /// Executes the initial scan logic. Must be implemented by derived classes.
    /// </summary>
    /// <returns>The initial table schema.</returns>
    protected abstract TableSchema ExecuteInitialScan();

    /// <summary>
    /// Executes the background scan logic. Must be implemented by derived classes.
    /// </summary>
    /// <param name="currentSchema">The current schema to refine.</param>
    /// <param name="cancellationToken">Token to cancel the scan.</param>
    /// <returns>The refined table schema.</returns>
    protected abstract TableSchema ExecuteBackgroundScan(
        TableSchema currentSchema,
        CancellationToken cancellationToken);
}

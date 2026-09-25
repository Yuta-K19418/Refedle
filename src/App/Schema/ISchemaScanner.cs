using Refedle.Engine.Models;

namespace Refedle.App.Schema;

/// <summary>
/// Scans a data file to infer its table schema: an initial scan over the first rows that
/// completes before the table is displayed, followed by a background refinement over the rest.
/// </summary>
internal interface ISchemaScanner
{
    /// <summary>
    /// Performs the initial scan that provides the first schema for display.
    /// </summary>
    /// <returns>The initial table schema.</returns>
    Task<TableSchema> InitialScanAsync();

    /// <summary>
    /// Starts the background scan that refines the schema with the remaining data.
    /// </summary>
    /// <param name="currentSchema">The schema produced by <see cref="InitialScanAsync"/>.</param>
    /// <param name="cancellationToken">Token to stop the background scan.</param>
    /// <returns>Task that completes with the final refined schema.</returns>
    Task<TableSchema> StartBackgroundScanAsync(
        TableSchema currentSchema,
        CancellationToken cancellationToken);
}

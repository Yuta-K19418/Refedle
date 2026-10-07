using Refedle.Engine;
using Refedle.Engine.Models;

namespace Refedle.App.Tui.Workers.Schema;

/// <summary>
/// Shared background schema refinement loop for incremental schema scanners.
/// </summary>
internal static class BackgroundSchemaScan
{
    /// <summary>
    /// Refines the schema with every row produced by a single forward pass over the file.
    /// </summary>
    /// <typeparam name="TRow">The type of a single row or line.</typeparam>
    /// <param name="currentSchema">The schema produced by the initial scan.</param>
    /// <param name="readRows">
    /// Lazily reads the rows to refine with, opening the file once per call and disposing it when
    /// the enumeration ends. Rows consumed by the initial scan must already be skipped.
    /// </param>
    /// <param name="refine">Refines the schema with a single row.</param>
    /// <param name="cancellationToken">Token to stop the scan.</param>
    /// <returns>The refined table schema.</returns>
    public static TableSchema Execute<TRow>(
        TableSchema currentSchema,
        Func<IEnumerable<TRow>> readRows,
        Func<TableSchema, TRow, Result<TableSchema>> refine,
        CancellationToken cancellationToken)
    {
        var refinedSchema = currentSchema;

        if (cancellationToken.IsCancellationRequested)
        {
            return refinedSchema;
        }

        foreach (var row in readRows())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var refineResult = refine(refinedSchema, row);
            if (refineResult.IsSuccess)
            {
                refinedSchema = refineResult.Value;
            }
        }

        return refinedSchema;
    }
}

using Refedle.Engine;
using Refedle.Engine.Models;

namespace Refedle.App.Tui.Workers.Schema;

/// <summary>
/// Shared background schema refinement loop for incremental schema scanners.
/// </summary>
internal static class BackgroundSchemaScan
{
    /// <summary>
    /// Refines the schema with every item after the initial scan, reading items in batches.
    /// </summary>
    /// <typeparam name="TItem">The type of a single row or line.</typeparam>
    /// <param name="currentSchema">The schema produced by the initial scan.</param>
    /// <param name="initialScanCount">The number of items already consumed by the initial scan.</param>
    /// <param name="batchSize">The maximum number of items read per batch.</param>
    /// <param name="readBatch">Reads a batch given the number of items to skip and the number to read.</param>
    /// <param name="refineSchema">Refines the schema with a single item.</param>
    /// <param name="cancellationToken">Token to stop the scan.</param>
    /// <returns>The refined table schema.</returns>
    public static TableSchema Execute<TItem>(
        TableSchema currentSchema,
        int initialScanCount,
        int batchSize,
        Func<int, int, IReadOnlyList<TItem>> readBatch,
        Func<TableSchema, TItem, Result<TableSchema>> refineSchema,
        CancellationToken cancellationToken)
    {
        var itemIndex = initialScanCount;
        var refinedSchema = currentSchema;

        while (!cancellationToken.IsCancellationRequested)
        {
            var items = readBatch(itemIndex, batchSize);
            if (items.Count == 0)
            {
                break;
            }

            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var refineResult = refineSchema(refinedSchema, item);
                if (refineResult.IsSuccess)
                {
                    refinedSchema = refineResult.Value;
                }
            }

            itemIndex += items.Count;
        }

        return refinedSchema;
    }
}

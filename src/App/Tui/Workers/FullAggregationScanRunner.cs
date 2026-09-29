using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.App.Tui.Workers;

/// <summary>
/// Runs <see cref="FullAggregationScanner.Scan"/> on a background thread.
/// </summary>
internal static class FullAggregationScanRunner
{
    /// <summary>
    /// Runs the full-aggregation DrillDown scan for <paramref name="filePath"/> on a background thread.
    /// </summary>
    /// <param name="filePath">The file to scan.</param>
    /// <param name="format">The file's data format.</param>
    /// <param name="keyPath">The KeyPath to traverse for every record.</param>
    /// <param name="ct">The cancellation token to observe.</param>
    /// <returns>The scan result once it completes.</returns>
    public static Task<Result<(TableSchema schema, IReadOnlyList<FocusedTableRow> rows)>> RunAsync(
        string filePath,
        DataFormat format,
        IReadOnlyList<KeyPathSegment> keyPath,
        CancellationToken ct)
    {
        return Task.Run(() => FullAggregationScanner.Scan(filePath, format, keyPath, ct), ct);
    }
}

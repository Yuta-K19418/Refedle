namespace Refedle.App.Tui.Ui.Views.JsonRangeTreeNodes;

/// <summary>
/// Stateless policy that determines how a large JSON file's rows are partitioned into range nodes
/// for lazy tree loading. Centralizing sizing logic decouples the tree views (which size node groups)
/// from the range tree nodes (which consume <see cref="RangeSize"/>), removing the bidirectional dependency
/// between a view and its node type.
/// </summary>
internal static class RangePartitionPolicy
{
    internal const int RangeSize = 1_000;
    private const int BytesPerRecord = 100;
    private const long SuperRangeThreshold = (long)RangeSize * RangeSize; // 1,000,000

    /// <summary>
    /// Calculates the node group size based on file size.
    /// Returns <see cref="RangeSize"/> for estimated rows ≤ 1,000,000,
    /// or a super-range size (multiple of <see cref="RangeSize"/>) for larger files.
    /// </summary>
    internal static long GetNodeGroupSize(long fileSize)
    {
        var estimatedRows = fileSize / BytesPerRecord;
        return estimatedRows > SuperRangeThreshold
            ? CalcSuperRangeSize(estimatedRows)
            : RangeSize;
    }

    private static long CalcSuperRangeSize(long estimatedRows)
    {
        var rangesCount = (estimatedRows + RangeSize - 1) / RangeSize;
        var superFactor = (rangesCount + RangeSize - 1) / RangeSize;
        return superFactor * RangeSize;
    }
}

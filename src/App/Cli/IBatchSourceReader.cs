namespace Refedle.App.Cli;

/// <summary>
/// Batch-oriented record source for the DrillDown record readers. ReadBatch mirrors the
/// (byteOffset, skip, fetch) shape of ElementReader.ReadElements and RowReader.ReadLines,
/// driven from a RowIndexer checkpoint. Implemented by thin structs so the generic
/// FullAggregationRecordReader&lt;TBatchSourceReader&gt; specializes without boxing.
/// </summary>
internal interface IBatchSourceReader : IDisposable
{
    /// <summary>
    /// Reads raw record bytes starting at the checkpoint <paramref name="byteOffset"/>.
    /// </summary>
    /// <param name="byteOffset">A checkpoint byte offset from RowIndexer.GetCheckPoint.</param>
    /// <param name="skip">Number of records to skip before collecting (non-negative).</param>
    /// <param name="fetch">Maximum records to collect after skipping (non-negative).</param>
    /// <returns>Raw JSON bytes per record.</returns>
    IReadOnlyList<JsonRawBytes> ReadBatch(long byteOffset, int skip, int fetch);
}

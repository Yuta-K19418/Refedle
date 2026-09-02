using Refedle.Engine.IO.JsonLines;

namespace Refedle.App.Cli;

/// <summary>
/// Adapts <see cref="RowReader"/> to <see cref="IBatchSourceReader"/> for JSON Lines DrillDown.
/// Unlike <see cref="JsonArrayBatchSourceReader"/> the reader is nullable: a zero-record JSON
/// Lines input is a zero-byte file, which <c>MmapService.Open</c> rejects, so <see cref="RowReader"/>
/// cannot be constructed. <see cref="FullAggregationRecordReader{TBatchSourceReader}"/> never calls
/// <see cref="ReadBatch"/> when <c>TotalRows == 0</c>, so the null case only surfaces via <c>?? []</c>.
/// </summary>
internal readonly struct JsonLinesBatchSourceReader(RowReader? reader) : IBatchSourceReader
{
    private readonly RowReader? _reader = reader;

    public IReadOnlyList<JsonRawBytes> ReadBatch(long byteOffset, int skip, int fetch) =>
        _reader?.ReadLines(byteOffset, skip, fetch) ?? [];

    public void Dispose() => _reader?.Dispose();
}

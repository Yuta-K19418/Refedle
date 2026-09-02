namespace Refedle.App.Cli;

/// <summary>
/// Union dispatch for JSON Lines reading: <c>[RecordReader(DataFormat.JsonLines)]</c> binds to
/// exactly one reader type, so the bare (non-DrillDown) and Full Aggregation DrillDown paths
/// share this struct. The active path is fixed at construction time (ADR-6).
/// </summary>
internal struct JsonLinesRecordReader : IRecordReader
{
    private readonly bool _isDrillDown;
    private BareJsonLinesRecordReader _bare;
    private FullAggregationRecordReader<JsonLinesBatchSourceReader> _drillDown;

    public JsonLinesRecordReader(BareJsonLinesRecordReader bare)
    {
        _isDrillDown = false;
        _bare = bare;
        _drillDown = default;
    }

    public JsonLinesRecordReader(FullAggregationRecordReader<JsonLinesBatchSourceReader> drillDown)
    {
        _isDrillDown = true;
        _bare = default;
        _drillDown = drillDown;
    }

    public ValueTask<bool> MoveNextAsync(CancellationToken ct) =>
        _isDrillDown ? _drillDown.MoveNextAsync(ct) : _bare.MoveNextAsync(ct);

    public readonly bool EvaluateFilters() =>
        _isDrillDown ? _drillDown.EvaluateFilters() : _bare.EvaluateFilters();

    public readonly CellData GetCellData(int outputColumnIndex) =>
        _isDrillDown ? _drillDown.GetCellData(outputColumnIndex) : _bare.GetCellData(outputColumnIndex);

    public void Dispose()
    {
        if (_isDrillDown)
        {
            _drillDown.Dispose();
            return;
        }

        _bare.Dispose();
    }
}

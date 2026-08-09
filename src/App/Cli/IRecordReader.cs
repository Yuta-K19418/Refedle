namespace Refedle.App.Cli;

internal interface IRecordReader : IDisposable
{
    ValueTask<bool> MoveNextAsync(CancellationToken ct);
    bool EvaluateFilters();

    /// <summary>
    /// Gets the cell data at the specified output column index for the current row.
    /// </summary>
    /// <remarks>
    /// The returned <see cref="CellData.Value"/> span is valid only until the next
    /// <see cref="GetCellData"/> call on this reader, and becomes invalid once this
    /// reader is disposed. Callers must consume the value before invoking this method
    /// again or disposing the reader.
    /// </remarks>
    CellData GetCellData(int outputColumnIndex);
}

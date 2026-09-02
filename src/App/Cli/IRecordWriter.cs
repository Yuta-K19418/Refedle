namespace Refedle.App.Cli;

internal interface IRecordWriter : IDisposable, IAsyncDisposable
{
    ValueTask WriteHeaderAsync(CancellationToken ct);
    ValueTask WriteStartRecordAsync(CancellationToken ct);
    void WriteCellData(int outputColumnIndex, CellData cell);
    ValueTask WriteEndRecordAsync(CancellationToken ct);

    /// <summary>
    /// Called once after the record loop ends, before <see cref="FlushAsync"/> — the counterpart
    /// to <see cref="WriteHeaderAsync"/>. Writers with no closing frame implement it as a no-op.
    /// </summary>
    ValueTask WriteFooterAsync(CancellationToken ct);

    ValueTask FlushAsync(CancellationToken ct);
}

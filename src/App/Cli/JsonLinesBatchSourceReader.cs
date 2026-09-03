using Refedle.Engine.IO.JsonLines;

namespace Refedle.App.Cli;

/// <summary>
/// Adapts <see cref="RowReader"/> to <see cref="IBatchSourceReader"/> for JSON Lines DrillDown.
/// Disposal delegates to RowReader's own guard, so a ReadBatch after Dispose throws the
/// reader's ObjectDisposedException.
/// </summary>
internal readonly struct JsonLinesBatchSourceReader(RowReader reader) : IBatchSourceReader
{
    private readonly RowReader _reader = reader;

    public IReadOnlyList<JsonRawBytes> ReadBatch(long byteOffset, int skip, int fetch) =>
        _reader.ReadLines(byteOffset, skip, fetch);

    public void Dispose() => _reader.Dispose();
}

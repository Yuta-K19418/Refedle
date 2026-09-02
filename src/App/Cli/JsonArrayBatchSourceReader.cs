using Refedle.Engine.IO.JsonArray;

namespace Refedle.App.Cli;

/// <summary>
/// Adapts <see cref="ElementReader"/> to <see cref="IBatchSourceReader"/> for JSON Array input.
/// Disposal delegates to ElementReader's own idempotent guard, so a ReadBatch after Dispose
/// throws the reader's ObjectDisposedException.
/// </summary>
internal readonly struct JsonArrayBatchSourceReader(ElementReader reader) : IBatchSourceReader
{
    private readonly ElementReader _reader = reader;

    public readonly IReadOnlyList<JsonRawBytes> ReadBatch(long byteOffset, int skip, int fetch) =>
        _reader.ReadElements(byteOffset, skip, fetch);

    public readonly void Dispose() => _reader.Dispose();
}

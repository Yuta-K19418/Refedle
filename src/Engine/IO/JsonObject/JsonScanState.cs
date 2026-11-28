using System.Text.Json;

namespace Refedle.Engine.IO.JsonObject;

internal ref struct JsonScanState
{
    public EntryState Entry = new();
    public BufferState Buffer;
    public JsonReaderState Checkpoint = new();
    public JsonReaderState RollbackCheckpoint = new();
    public JsonReaderState PrevCheckpoint = new();

    public JsonScanState(long fileSize, long initialReadHead = 0)
    {
        Buffer = new(fileSize, initialReadHead);
    }
}

using System.Text.Json;

namespace Refedle.Engine.IO.JsonObject;

internal ref struct JsonScanState(long fileSize, long initialReadHead = 0)
{
    public EntryState Entry = new();
    public BufferState Buffer = new(fileSize, initialReadHead);
    public JsonReaderState Checkpoint = new();
    public JsonReaderState RollbackCheckpoint = new();
    public JsonReaderState PrevCheckpoint = new();
}

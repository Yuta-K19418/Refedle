namespace Refedle.Engine.IO.JsonObject;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal ref struct BufferState(long fileSize)
{
    private readonly long _fileSize = fileSize;

    private long _readHead;
    private int _remainingLen;

    public readonly int RemainingLen => _remainingLen;
    public readonly long ReadHead => _readHead;
    public readonly long BufferOffset => _readHead - _remainingLen;
    public readonly bool IsFinalBlock => _readHead >= _fileSize;

    public void RecordBytesRead(int bytesRead)
    {
        _readHead += bytesRead;
        _remainingLen += bytesRead;
    }

    public void ShiftConsumed(int safeConsumed, byte[] buffer)
    {
        _remainingLen -= safeConsumed;
        Buffer.BlockCopy(
            src: buffer,
            srcOffset: safeConsumed,
            dst: buffer,
            dstOffset: 0,
            count: _remainingLen
        );
    }
}

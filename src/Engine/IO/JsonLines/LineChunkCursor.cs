using System.Buffers;

namespace Refedle.Engine.IO.JsonLines;

/// <summary>
/// Cuts newline-terminated lines out of a memory-mapped file by reading it forward in large chunks,
/// so each byte is copied out of the file once instead of once per line.
/// The chunk buffer is borrowed from <see cref="ArrayPool{T}"/> and returned on <see cref="Dispose"/>.
/// A cursor is created per read operation and must not be shared between threads.
/// </summary>
/// <param name="mmap">The memory-mapped file to read from.</param>
/// <param name="startOffset">Byte offset of the first line to cut.</param>
internal sealed class LineChunkCursor(MmapService mmap, long startOffset) : IDisposable
{
    // 64 KB: far larger than a typical JSON line, so one read yields many lines,
    // yet small enough to stay cache-resident and to be served from the shared pool.
    private const int ChunkSize = 64 * 1024;

    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
    private int _head;
    private int _tail;
    private long _nextFileOffset = startOffset;

    /// <summary>
    /// Advances past the next newline-terminated line without materializing it.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if a newline-terminated line was skipped;
    /// <see langword="false"/> if the file ends first (an unterminated trailing line does not count).
    /// </returns>
    /// <exception cref="NotSupportedException">The line exceeds the supported size limit.</exception>
    public bool TrySkipLine()
    {
        var lineBytes = 0L;

        while (true)
        {
            var pending = _buffer.AsSpan(_head, _tail - _head);
            var newlineIndex = pending.IndexOf((byte)'\n');
            if (newlineIndex != -1)
            {
                lineBytes += newlineIndex + 1;
                ThrowIfLineTooLong(lineBytes);
                _head += newlineIndex + 1;
                return true;
            }

            lineBytes += pending.Length;
            _head = _tail;
            if (!Fill())
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Reads the next line with its trailing <c>\n</c> or <c>\r\n</c> removed.
    /// A trailing line without a newline is returned as the last line; an empty remainder yields no line.
    /// </summary>
    /// <param name="line">The line bytes, copied out of the chunk buffer.</param>
    /// <returns><see langword="true"/> if a line was read; <see langword="false"/> at the end of the file.</returns>
    /// <exception cref="NotSupportedException">The line exceeds the supported size limit.</exception>
    public bool TryReadLine(out JsonRawBytes line)
    {
        var scannedBytes = 0;

        while (true)
        {
            var unscanned = _buffer.AsSpan(_head + scannedBytes, _tail - _head - scannedBytes);
            var newlineIndex = unscanned.IndexOf((byte)'\n');
            if (newlineIndex != -1)
            {
                line = CutLine(scannedBytes + newlineIndex + 1);
                return true;
            }

            scannedBytes = _tail - _head;
            if (Fill())
            {
                continue;
            }

            // End of file: a trailing line without a newline is the last line
            line = CutLine(scannedBytes);
            return line.Length > 0;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = [];
    }

    private static ReadOnlySpan<byte> TrimNewline(ReadOnlySpan<byte> span)
    {
        // Remove trailing \r\n or \n
        if (span.Length > 0 && span[span.Length - 1] == '\n')
        {
            span = span[..^1];
            if (span.Length > 0 && span[span.Length - 1] == '\r')
            {
                span = span[..^1];
            }
        }

        return span;
    }

    private static void ThrowIfLineTooLong(long lineBytes)
    {
        // Lines exceeding ~2 GB are not currently supported; revisit if demand arises.
        if (lineBytes > Array.MaxLength)
        {
            throw new NotSupportedException("JSON line exceeds maximum supported size.");
        }
    }

    private JsonRawBytes CutLine(int lineLength)
    {
        var trimmed = TrimNewline(_buffer.AsSpan(_head, lineLength));
        _head += lineLength;
        return trimmed.ToArray();
    }

    // Appends the next part of the file after the unconsumed bytes; returns false at end of file.
    private bool Fill()
    {
        if (mmap.Length - _nextFileOffset <= 0)
        {
            return false;
        }

        if (_head == _tail)
        {
            _head = 0;
            _tail = 0;
        }

        if (_tail == _buffer.Length)
        {
            MakeRoom();
        }

        // Loading a whole line moves the read position, so the remaining length is taken after making room
        var remaining = mmap.Length - _nextFileOffset;
        var readLength = (int)Math.Min(remaining, Math.Min(ChunkSize, _buffer.Length - _tail));
        mmap.Read(_nextFileOffset, _buffer.AsSpan(_tail, readLength));
        _nextFileOffset += readLength;
        _tail += readLength;
        return true;
    }

    // Keeps the unconsumed remainder of a line split at the chunk boundary at the front of the buffer,
    // or, when that remainder alone fills the buffer (a line longer than the buffer), loads the whole line.
    private void MakeRoom()
    {
        if (_head > 0)
        {
            var remainder = _buffer.AsSpan(_head, _tail - _head);
            remainder.CopyTo(_buffer);
            _tail -= _head;
            _head = 0;
            return;
        }

        LoadWholeLine();
    }

    // Determines the total length of the line at the front of the buffer before allocating for it,
    // so an unsupported length is rejected without first allocating a buffer of that size.
    private void LoadWholeLine()
    {
        var lineStartOffset = _nextFileOffset - (_tail - _head);
        var lineLength = MeasureLine(lineStartOffset);
        ThrowIfLineTooLong(lineLength);

        // The buffer is full without a newline and the file continues, so the line is always longer than the buffer
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = ArrayPool<byte>.Shared.Rent((int)lineLength);

        mmap.Read(lineStartOffset, _buffer.AsSpan(0, (int)lineLength));
        _head = 0;
        _tail = (int)lineLength;
        _nextFileOffset = lineStartOffset + lineLength;
    }

    // Scans forward through the file for the end of the line without keeping its bytes;
    // a line without a newline runs to the end of the file.
    private long MeasureLine(long lineStartOffset)
    {
        var lineLength = 0L;

        while (true)
        {
            var offset = lineStartOffset + lineLength;
            var remaining = mmap.Length - offset;
            if (remaining <= 0)
            {
                return lineLength;
            }

            var readLength = (int)Math.Min(remaining, _buffer.Length);
            var scanned = _buffer.AsSpan(0, readLength);
            mmap.Read(offset, scanned);

            var newlineIndex = scanned.IndexOf((byte)'\n');
            if (newlineIndex != -1)
            {
                return lineLength + newlineIndex + 1;
            }

            lineLength += readLength;
        }
    }
}

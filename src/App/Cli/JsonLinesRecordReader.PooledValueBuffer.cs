using System.Buffers;

namespace Refedle.App.Cli;

internal partial struct JsonLinesRecordReader
{
    // Wraps the rented ArrayPool<char> buffer in a reference type, assigned once in the
    // constructor, so every struct copy shares one buffer identity and one disposal path
    // (mirrors _rowReader). See docs/design_jsonlines_cell_value_zero_alloc.md step 1.
    private sealed class PooledValueBuffer : IDisposable
    {
        private const int MinimumSize = 256;

        private char[]? _buffer;
        private bool _disposed;

        // Wired in Step 2 (ReadPropertyValue replacement); unused in this scaffold step.
        public char[] Reserve(int minimumLength)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_buffer is not null && _buffer.Length >= minimumLength)
            {
                return _buffer;
            }

            if (_buffer is not null)
            {
                ArrayPool<char>.Shared.Return(_buffer);
            }

            _buffer = ArrayPool<char>.Shared.Rent(Math.Max(MinimumSize, minimumLength));
            return _buffer;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_buffer is not null)
            {
                ArrayPool<char>.Shared.Return(_buffer);
                _buffer = null;
            }

            _disposed = true;
        }
    }
}

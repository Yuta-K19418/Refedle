using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

public sealed partial class RecordProcessorTests
{
    private struct TestRecordWriter : IRecordWriter
    {
        public Action? WriteHeaderCallback;
        public Action<string[]>? WriteCellCallback;
        public Action? WriteFooterCallback;
        public Action? FlushCallback;
        private readonly List<string> _cells;

        public TestRecordWriter(
            Action? writeHeaderCallback = null,
            Action<string[]>? writeCellCallback = null,
            Action? writeFooterCallback = null,
            Action? flushCallback = null)
        {
            WriteHeaderCallback = writeHeaderCallback;
            WriteCellCallback = writeCellCallback;
            WriteFooterCallback = writeFooterCallback;
            FlushCallback = flushCallback;
            _cells = [];
        }

        public readonly void Dispose() { }
        public readonly ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public readonly ValueTask WriteHeaderAsync(CancellationToken ct)
        {
            WriteHeaderCallback?.Invoke();
            return ValueTask.CompletedTask;
        }

        public readonly ValueTask WriteStartRecordAsync(CancellationToken ct)
        {
            _cells.Clear();
            return ValueTask.CompletedTask;
        }

        public readonly void WriteCellData(int outputColumnIndex, CellData cell)
        {
            _cells.Add(cell.Value.ToString());
        }

        public readonly ValueTask WriteEndRecordAsync(CancellationToken ct)
        {
            WriteCellCallback?.Invoke([.. _cells]);
            return ValueTask.CompletedTask;
        }

        public readonly ValueTask WriteFooterAsync(CancellationToken ct)
        {
            WriteFooterCallback?.Invoke();
            return ValueTask.CompletedTask;
        }

        public readonly ValueTask FlushAsync(CancellationToken ct)
        {
            FlushCallback?.Invoke();
            return ValueTask.CompletedTask;
        }
    }
}

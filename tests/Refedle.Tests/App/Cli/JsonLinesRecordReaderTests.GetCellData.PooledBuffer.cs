using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.App.Cli;

public sealed partial class JsonLinesRecordReaderTests
{
    [Fact]
    public async Task GetCellData_MultipleColumnsReadSequentially_KeepsEachValueValidWhenRead()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"number":1.50,"text":"hello"}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputColumnNames, outputSchema) = BuildSchemas(["number", "text"]);
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema);
            await reader.MoveNextAsync(default);

            // Act — consume each cell immediately (the RecordProcessor pattern) so buffer
            // reuse across calls cannot corrupt a value before it is read.
            var first = reader.GetCellData(0).Value.ToString();
            var second = reader.GetCellData(1).Value.ToString();

            // Assert
            first.Should().Be("1.50");
            second.Should().Be("hello");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_StringExceedingInitialBuffer_ReturnsFullValue()
    {
        // Arrange
        var longValue = new string('x', 300); // > MinimumSize (256), forces buffer growth
        var filePath = Path.GetTempFileName();
        try
        {
            var content = """{"value":"hi"}""" + "\n" + $$"""{"value":"{{longValue}}"}""";
            File.WriteAllText(filePath, content);
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputColumnNames, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema);

            // Establish the initial 256-char buffer with the short first row.
            await reader.MoveNextAsync(default);
            _ = reader.GetCellData(0).Value.Length;

            // Advance to the long row, which exceeds the initial buffer and forces growth.
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Value);
            cell.Encoding.Should().Be(CellEncoding.PlainText);
            cell.Value.ToString().Should().Be(longValue);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_StringWithEscapeSequences_ReturnsResolvedText()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            // Raw JSON escapes (\n, \"); CopyString must resolve them, not return them verbatim.
            File.WriteAllText(filePath, """{"value":"line1\n\"quoted\""}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputColumnNames, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Value);
            cell.Encoding.Should().Be(CellEncoding.PlainText);
            cell.Value.ToString().Should().Be("line1\n\"quoted\"");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_EmptyString_ReturnsEmptyValueWithoutError()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            // Empty string exercises Reserve's Math.Max(MinimumSize, 0) floor without error.
            File.WriteAllText(filePath, """{"value":""}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputColumnNames, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Value);
            cell.Encoding.Should().Be(CellEncoding.PlainText);
            cell.Value.ToString().Should().Be(string.Empty);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_AfterDisposingOriginalCopy_ThrowsObjectDisposedException()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"value":1.50}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputColumnNames, outputSchema) = BuildSchemas();
            var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema);
            await reader.MoveNextAsync(default);

            // The copy keeps its own _disposed=false but shares the reference-type buffer, so
            // only Reserve's guard (not the copy's ThrowIfDisposed) catches the disposed shared
            // buffer. Disposing the same instance instead would be caught too early to test it.
            var copy = reader;
            reader.Dispose();

            // Act
            Action act = () => { _ = copy.GetCellData(0); };

            // Assert
            act.Should().Throw<ObjectDisposedException>();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_AfterWarmUp_AllocatesZeroBytes()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"value":1.50}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputColumnNames, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema);
            await reader.MoveNextAsync(default);

            // Warm up so the one-time pooled-buffer Reserve happens before measurement.
            _ = reader.GetCellData(0).Value.Length;

            // Act — steady-state GetCellData reuses the buffer and must allocate nothing.
            // Same thread throughout: MoveNextAsync completes synchronously, so no await hop.
            var before = GC.GetAllocatedBytesForCurrentThread();
            var cell = reader.GetCellData(0);
            var after = GC.GetAllocatedBytesForCurrentThread();

            // Assert
            (after - before).Should().Be(0);
            cell.Presence.Should().Be(CellPresence.Value);
            cell.Value.Length.Should().Be("1.50".Length);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_StringWithUtf8Characters_ReturnsFullText()
    {
        // Arrange
        var value = "日本語😀"; // CJK characters plus a surrogate-pair emoji
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, $$"""{"value":"{{value}}"}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputColumnNames, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Value);
            cell.Encoding.Should().Be(CellEncoding.PlainText);
            cell.Value.ToString().Should().Be(value);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData("""{"text":"日本語😀"}""")]
    [InlineData("""["日本語😀"]""")]
    public async Task GetCellData_RawStructuredValueWithUtf8_ReturnsFullRawText(string rawValue)
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, $$"""{"value":{{rawValue}}}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputColumnNames, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Value);
            cell.Encoding.Should().Be(CellEncoding.Raw);
            cell.Value.ToString().Should().Be(rawValue);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}

using System.Text;
using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli;

public sealed class JsonLinesRecordWriterTests
{
    private static readonly BatchOutputSchema _outputSchema = new(
        [new BatchOutputColumn("value", "value")],
        []);

    // Cell-encoding branches are covered by JsonCellWriterTests; this exercises the JSON Lines
    // record framing (one object per line, trailing newline) and the delegation to JsonCellWriter.
    [Fact]
    public async Task WriteRecord_WithSingleCell_WritesJsonObjectLineWithNewline()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new JsonLinesRecordWriter(stream, _outputSchema);
        await writer.WriteStartRecordAsync(default);

        // Act
        writer.WriteCellData(0, new CellData("hello", CellPresence.Value, CellEncoding.PlainText));
        await writer.WriteEndRecordAsync(default);
        await writer.FlushAsync(default);

        // Assert
        var output = Encoding.UTF8.GetString(stream.ToArray());
        output.Should().Be("{\"value\":\"hello\"}\n");
    }

    [Fact]
    public async Task WriteMultipleRecords_WritesOneObjectPerLine()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new JsonLinesRecordWriter(stream, _outputSchema);

        // Act
        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData("a", CellPresence.Value, CellEncoding.PlainText));
        await writer.WriteEndRecordAsync(default);
        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData("b", CellPresence.Value, CellEncoding.PlainText));
        await writer.WriteEndRecordAsync(default);
        await writer.WriteFooterAsync(default);
        await writer.FlushAsync(default);

        // Assert
        var output = Encoding.UTF8.GetString(stream.ToArray());
        output.Should().Be("{\"value\":\"a\"}\n{\"value\":\"b\"}\n");
    }

    [Fact]
    public async Task WriteFooterAsync_IsNoOp()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new JsonLinesRecordWriter(stream, _outputSchema);
        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData("x", CellPresence.Value, CellEncoding.PlainText));
        await writer.WriteEndRecordAsync(default);

        // Act
        await writer.WriteFooterAsync(default);
        await writer.FlushAsync(default);

        // Assert — no closing frame is appended.
        var output = Encoding.UTF8.GetString(stream.ToArray());
        output.Should().Be("{\"value\":\"x\"}\n");
    }

    [Fact]
    public void ThrowIfDisposed_AfterDispose_ThrowsWithConcreteTypeName()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new JsonLinesRecordWriter(stream, _outputSchema);
        writer.Dispose();

        // Act
        Action act = () => writer.ThrowIfDisposed();

        // Assert
        var exception = act.Should().Throw<ObjectDisposedException>().Which;
        exception.ObjectName.Should().Be(typeof(JsonLinesRecordWriter).FullName);
    }
}

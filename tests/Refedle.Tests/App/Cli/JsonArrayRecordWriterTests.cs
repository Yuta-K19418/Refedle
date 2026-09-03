using System.Text;
using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli;

public sealed class JsonArrayRecordWriterTests
{
    private static readonly BatchOutputSchema _oneColumn = new(
        [new BatchOutputColumn("value", "value")],
        []);

    [Fact]
    public async Task WriteArray_WithNoRecords_WritesEmptyArray()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new JsonArrayRecordWriter(stream, _oneColumn);

        // Act
        await writer.WriteHeaderAsync(default);
        await writer.WriteFooterAsync(default);
        await writer.FlushAsync(default);

        // Assert
        Encoding.UTF8.GetString(stream.ToArray()).Should().Be("[]");
    }

    [Fact]
    public async Task WriteArray_WithSingleRecord_WrapsTheObjectInArrayBrackets()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new JsonArrayRecordWriter(stream, _oneColumn);

        // Act
        await writer.WriteHeaderAsync(default);
        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData("a", CellPresence.Value, CellEncoding.PlainText));
        await writer.WriteEndRecordAsync(default);
        await writer.WriteFooterAsync(default);
        await writer.FlushAsync(default);

        // Assert
        Encoding.UTF8.GetString(stream.ToArray()).Should().Be("""[{"value":"a"}]""");
    }

    [Fact]
    public async Task WriteArray_WithMultipleRecords_SeparatesObjectsWithCommas()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new JsonArrayRecordWriter(stream, _oneColumn);

        // Act
        await writer.WriteHeaderAsync(default);
        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData("a", CellPresence.Value, CellEncoding.PlainText));
        await writer.WriteEndRecordAsync(default);
        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData("b", CellPresence.Value, CellEncoding.PlainText));
        await writer.WriteEndRecordAsync(default);
        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData("c", CellPresence.Value, CellEncoding.PlainText));
        await writer.WriteEndRecordAsync(default);
        await writer.WriteFooterAsync(default);
        await writer.FlushAsync(default);

        // Assert
        Encoding.UTF8.GetString(stream.ToArray())
            .Should().Be("""[{"value":"a"},{"value":"b"},{"value":"c"}]""");
    }

    [Fact]
    public async Task WriteArray_WithMultiColumnRecord_WritesEveryColumnAsAProperty()
    {
        // Arrange
        var twoColumns = new BatchOutputSchema(
            [new BatchOutputColumn("id", "id"), new BatchOutputColumn("name", "name")],
            []);
        using var stream = new MemoryStream();
        using var writer = new JsonArrayRecordWriter(stream, twoColumns);

        // Act
        await writer.WriteHeaderAsync(default);
        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData("1", CellPresence.Value, CellEncoding.Numeric));
        writer.WriteCellData(1, new CellData("Alice", CellPresence.Value, CellEncoding.PlainText));
        await writer.WriteEndRecordAsync(default);
        await writer.WriteFooterAsync(default);
        await writer.FlushAsync(default);

        // Assert
        Encoding.UTF8.GetString(stream.ToArray()).Should().Be("""[{"id":1,"name":"Alice"}]""");
    }

    [Fact]
    public async Task WriteArray_WithMissingCell_WritesValidEmptyObjectWithoutBreakingFraming()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new JsonArrayRecordWriter(stream, _oneColumn);

        // Act
        await writer.WriteHeaderAsync(default);
        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData([], CellPresence.Missing));
        await writer.WriteEndRecordAsync(default);
        await writer.WriteFooterAsync(default);
        await writer.FlushAsync(default);

        // Assert
        Encoding.UTF8.GetString(stream.ToArray()).Should().Be("[{}]");
    }

    [Fact]
    public void ThrowIfDisposed_AfterDispose_ThrowsWithConcreteTypeName()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new JsonArrayRecordWriter(stream, _oneColumn);
        writer.Dispose();

        // Act
        Action act = () => writer.ThrowIfDisposed();

        // Assert
        var exception = act.Should().Throw<ObjectDisposedException>().Which;
        exception.ObjectName.Should().Be(typeof(JsonArrayRecordWriter).FullName);
    }

    [Fact]
    public async Task WriteFooterAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new JsonArrayRecordWriter(stream, _oneColumn);
        writer.Dispose();

        // Act
        var act = async () => await writer.WriteFooterAsync(default);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task WriteFooterAsync_AfterDisposeAsync_ThrowsObjectDisposedException()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new JsonArrayRecordWriter(stream, _oneColumn);
        await writer.DisposeAsync();

        // Act
        var act = async () => await writer.WriteFooterAsync(default);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}

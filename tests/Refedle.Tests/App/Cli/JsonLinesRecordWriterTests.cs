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

    // CellPresence/CellEncoding are internal, so each Presence x Encoding combination
    // is a standalone case (the enums are constructed inside the test body in Step 2).
    [Fact]
    public async Task WriteCellData_ValuePlainText_WritesJsonString()
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
    public async Task WriteCellData_ValueRaw_WritesRawJson()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new JsonLinesRecordWriter(stream, _outputSchema);
        await writer.WriteStartRecordAsync(default);

        // Act
        writer.WriteCellData(0, new CellData("""{"nested":1}""", CellPresence.Value, CellEncoding.Raw));
        await writer.WriteEndRecordAsync(default);
        await writer.FlushAsync(default);

        // Assert
        var output = Encoding.UTF8.GetString(stream.ToArray());
        output.Should().Be("{\"value\":{\"nested\":1}}\n");
    }

    [Fact]
    public async Task WriteCellData_ValueNumeric_WritesJsonNumber()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new JsonLinesRecordWriter(stream, _outputSchema);
        await writer.WriteStartRecordAsync(default);

        // Act
        writer.WriteCellData(0, new CellData("007", CellPresence.Value, CellEncoding.Numeric));
        await writer.WriteEndRecordAsync(default);
        await writer.FlushAsync(default);

        // Assert
        var output = Encoding.UTF8.GetString(stream.ToArray());
        output.Should().Be("{\"value\":7}\n");
    }

    [Fact]
    public async Task WriteCellData_ValueBoolean_WritesJsonBoolean()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new JsonLinesRecordWriter(stream, _outputSchema);
        await writer.WriteStartRecordAsync(default);

        // Act
        writer.WriteCellData(0, new CellData("TRUE", CellPresence.Value, CellEncoding.Boolean));
        await writer.WriteEndRecordAsync(default);
        await writer.FlushAsync(default);

        // Assert
        var output = Encoding.UTF8.GetString(stream.ToArray());
        output.Should().Be("{\"value\":true}\n");
    }

    [Fact]
    public async Task WriteCellData_Null_WritesJsonNull()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new JsonLinesRecordWriter(stream, _outputSchema);
        await writer.WriteStartRecordAsync(default);

        // Act
        writer.WriteCellData(0, new CellData([], CellPresence.Null));
        await writer.WriteEndRecordAsync(default);
        await writer.FlushAsync(default);

        // Assert
        var output = Encoding.UTF8.GetString(stream.ToArray());
        output.Should().Be("{\"value\":null}\n");
    }

    [Fact]
    public async Task WriteCellData_Missing_OmitsProperty()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new JsonLinesRecordWriter(stream, _outputSchema);
        await writer.WriteStartRecordAsync(default);

        // Act
        writer.WriteCellData(0, new CellData([], CellPresence.Missing));
        await writer.WriteEndRecordAsync(default);
        await writer.FlushAsync(default);

        // Assert
        var output = Encoding.UTF8.GetString(stream.ToArray());
        output.Should().Be("{}\n");
    }

    [Fact]
    public async Task WriteCellData_Invalid_WritesEmptyString()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var writer = new JsonLinesRecordWriter(stream, _outputSchema);
        await writer.WriteStartRecordAsync(default);

        // Act
        writer.WriteCellData(0, new CellData([], CellPresence.Invalid));
        await writer.WriteEndRecordAsync(default);
        await writer.FlushAsync(default);

        // Assert
        var output = Encoding.UTF8.GetString(stream.ToArray());
        output.Should().Be("{\"value\":\"\"}\n");
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

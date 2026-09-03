using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli;

public sealed class JsonCellWriterTests
{
    private static readonly BatchOutputSchema _outputSchema = new(
        [new BatchOutputColumn("value", "value")],
        []);

    // CellPresence/CellEncoding are internal, so each Presence x Encoding combination is a standalone case.
    [Fact]
    public void WriteCellData_ValuePlainText_WritesJsonString()
    {
        // Arrange
        var cell = new CellData("hello", CellPresence.Value, CellEncoding.PlainText);

        // Act
        var json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":"hello"}""");
    }

    [Fact]
    public void WriteCellData_ValuePlainTextWithSpecialCharacters_EscapesQuotesAndControlCharsAndKeepsNonAscii()
    {
        // Arrange — quote and newline must be JSON-escaped; non-ASCII passes through unescaped.
        var cell = new CellData("he said \"hi\"\ncafé 日本語", CellPresence.Value, CellEncoding.PlainText);

        // Act
        var json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("{\"value\":\"he said \\\"hi\\\"\\ncafé 日本語\"}");
    }

    [Fact]
    public void WriteCellData_ValueRaw_WritesRawJson()
    {
        // Arrange
        var cell = new CellData("""{"nested":1}""", CellPresence.Value, CellEncoding.Raw);

        // Act
        var json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":{"nested":1}}""");
    }

    [Fact]
    public void WriteCellData_ValueNumericInteger_WritesJsonNumber()
    {
        // Arrange
        var cell = new CellData("007", CellPresence.Value, CellEncoding.Numeric);

        // Act
        var json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":7}""");
    }

    [Fact]
    public void WriteCellData_ValueNumericFloatingPoint_WritesJsonNumber()
    {
        // Arrange
        var cell = new CellData("3.50", CellPresence.Value, CellEncoding.Numeric);

        // Act
        var json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":3.5}""");
    }

    [Fact]
    public void WriteCellData_ValueBoolean_WritesJsonBoolean()
    {
        // Arrange
        var cell = new CellData("TRUE", CellPresence.Value, CellEncoding.Boolean);

        // Act
        var json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":true}""");
    }

    [Fact]
    public void WriteCellData_Null_WritesJsonNull()
    {
        // Arrange
        var cell = new CellData([], CellPresence.Null);

        // Act
        var json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":null}""");
    }

    [Fact]
    public void WriteCellData_Invalid_WritesEmptyString()
    {
        // Arrange
        var cell = new CellData([], CellPresence.Invalid);

        // Act
        var json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":""}""");
    }

    [Fact]
    public void WriteCellData_Missing_OmitsProperty()
    {
        // Arrange
        var cell = new CellData([], CellPresence.Missing);

        // Act
        var json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("{}");
    }

    private static string WriteSingleCell(CellData cell)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(
            buffer,
            new JsonWriterOptions { SkipValidation = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        writer.WriteStartObject();
        JsonCellWriter.WriteCellData(writer, _outputSchema, 0, cell);
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}

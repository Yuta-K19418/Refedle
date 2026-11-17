using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.App.Cli.IO;
using Refedle.App.Cli.IO.Json;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli.IO.Json;

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
        CellData cell = new("hello", CellPresence.Value, CellEncoding.PlainText);

        // Act
        string json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":"hello"}""");
    }

    [Fact]
    public void WriteCellData_ValuePlainTextWithSpecialCharacters_EscapesQuotesAndControlCharsAndKeepsNonAscii()
    {
        // Arrange — quote and newline must be JSON-escaped; non-ASCII passes through unescaped.
        CellData cell = new("he said \"hi\"\ncafé 日本語", CellPresence.Value, CellEncoding.PlainText);

        // Act
        string json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("{\"value\":\"he said \\\"hi\\\"\\ncafé 日本語\"}");
    }

    [Fact]
    public void WriteCellData_ValueRaw_WritesRawJson()
    {
        // Arrange
        CellData cell = new("""{"nested":1}""", CellPresence.Value, CellEncoding.Raw);

        // Act
        string json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":{"nested":1}}""");
    }

    [Fact]
    public void WriteCellData_ValueNumericInteger_WritesJsonNumber()
    {
        // Arrange
        CellData cell = new("007", CellPresence.Value, CellEncoding.Numeric);

        // Act
        string json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":7}""");
    }

    [Fact]
    public void WriteCellData_ValueNumericFloatingPoint_WritesJsonNumber()
    {
        // Arrange
        CellData cell = new("3.50", CellPresence.Value, CellEncoding.Numeric);

        // Act
        string json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":3.5}""");
    }

    [Fact]
    public void WriteCellData_ValueBoolean_WritesJsonBoolean()
    {
        // Arrange
        CellData cell = new("TRUE", CellPresence.Value, CellEncoding.Boolean);

        // Act
        string json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":true}""");
    }

    [Fact]
    public void WriteCellData_Null_WritesJsonNull()
    {
        // Arrange
        CellData cell = new([], CellPresence.Null);

        // Act
        string json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":null}""");
    }

    [Fact]
    public void WriteCellData_Invalid_WritesEmptyString()
    {
        // Arrange
        CellData cell = new([], CellPresence.Invalid);

        // Act
        string json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("""{"value":""}""");
    }

    [Fact]
    public void WriteCellData_Missing_OmitsProperty()
    {
        // Arrange
        CellData cell = new([], CellPresence.Missing);

        // Act
        string json = WriteSingleCell(cell);

        // Assert
        json.Should().Be("{}");
    }

    private static string WriteSingleCell(CellData cell)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(
            buffer,
            new JsonWriterOptions { SkipValidation = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        writer.WriteStartObject();
        JsonCellWriter.WriteCellData(writer, _outputSchema, 0, cell);
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}

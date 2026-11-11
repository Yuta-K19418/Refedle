using AwesomeAssertions;
using Refedle.Engine.IO.Json;

namespace Refedle.Tests.Engine.IO.Json;

public sealed class JsonObjectCellExtractorTests
{
    [Fact]
    public void ExtractCell_StringValue_ReturnsUnquotedString()
    {
        // Arrange
        var line = "{\"name\": \"Alice\"}"u8;
        var columnName = "name"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("Alice");
    }

    [Fact]
    public void ExtractCell_EscapedStringValue_ResolvesEscapeSequences()
    {
        // Arrange
        var line = "{\"memo\": \"caf\\u00e9 \\u8336\\n\\\"quoted\\\" \\ud83d\\ude00\"}"u8;
        var columnName = "memo"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("café 茶\n\"quoted\" 😀");
    }

    [Fact]
    public void ExtractCell_EscapedValueWithInvalidUtf8_ReturnsRawText()
    {
        // Arrange: the value token contains both an escape and an invalid UTF-8 byte (0xFF)
        byte[] line = [.. "{\"memo\": \"a\\nb"u8, 0xFF, .. "\"}"u8];
        var columnName = "memo"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("a\\nb\uFFFD");
    }

    [Fact]
    public void ExtractCell_UnescapedInvalidUtf8Value_ReturnsReplacementText()
    {
        // Arrange: bytes 0xC3 0x28 are not a valid UTF-8 sequence
        byte[] line = [.. "{\"memo\": \""u8, 0xC3, 0x28, .. "\"}"u8];
        var columnName = "memo"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("\uFFFD(");
    }

    [Fact]
    public void ExtractCell_IntegerValue_ReturnsNumberAsString()
    {
        // Arrange
        var line = "{\"id\": 42}"u8;
        var columnName = "id"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("42");
    }

    [Fact]
    public void ExtractCell_DecimalValue_ReturnsNumberAsString()
    {
        // Arrange
        var line = "{\"price\": 3.14}"u8;
        var columnName = "price"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("3.14");
    }

    [Fact]
    public void ExtractCell_TrueValue_ReturnsTrueString()
    {
        // Arrange
        var line = "{\"active\": true}"u8;
        var columnName = "active"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("True");
    }

    [Fact]
    public void ExtractCell_FalseValue_ReturnsFalseString()
    {
        // Arrange
        var line = "{\"active\": false}"u8;
        var columnName = "active"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("False");
    }

    [Fact]
    public void ExtractCell_NullValue_ReturnsNullPlaceholder()
    {
        // Arrange
        var line = "{\"value\": null}"u8;
        var columnName = "value"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("<null>");
    }

    [Fact]
    public void ExtractCell_NestedObject_ReturnsCollapsedPreview()
    {
        // Arrange
        var line = "{\"address\": {\"city\": \"Tokyo\"}}"u8;
        var columnName = "address"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("{Object: 1 properties}");
    }

    [Fact]
    public void ExtractCell_Array_ReturnsCollapsedPreview()
    {
        // Arrange
        var line = "{\"tags\": [\"a\", \"b\"]}"u8;
        var columnName = "tags"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("[Array: 2 items]");
    }

    [Fact]
    public void ExtractCell_EmptyObject_ReturnsCollapsedPreview()
    {
        // Arrange
        var line = "{\"address\": {}}"u8;
        var columnName = "address"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("{Object: 0 properties}");
    }

    [Fact]
    public void ExtractCell_EmptyArray_ReturnsCollapsedPreview()
    {
        // Arrange
        var line = "{\"tags\": []}"u8;
        var columnName = "tags"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("[Array: 0 items]");
    }

    [Fact]
    public void ExtractCell_ObjectWithNestedContainer_ReturnsDirectPropertyCount()
    {
        // Arrange
        var line = "{\"address\": {\"city\": \"Tokyo\", \"coords\": {\"lat\": 1, \"lng\": 2}}}"u8;
        var columnName = "address"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("{Object: 2 properties}");
    }

    [Fact]
    public void ExtractCell_MissingKey_ReturnsNullPlaceholder()
    {
        // Arrange
        var line = "{\"id\": 1}"u8;
        var columnName = "name"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("<null>");
    }

    [Fact]
    public void ExtractCell_MalformedJson_ReturnsErrorPlaceholder()
    {
        // Arrange
        var line = "not-json"u8;
        var columnName = "id"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("<error>");
    }

    [Fact]
    public void ExtractCell_EmptyLine_ReturnsErrorPlaceholder()
    {
        // Arrange
        ReadOnlySpan<byte> line = [];
        var columnName = "id"u8;

        // Act
        var result = JsonObjectCellExtractor.ExtractCell(line, columnName);

        // Assert
        result.Should().Be("<error>");
    }
}

using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.Engine.IO.Json;

namespace Refedle.Tests.Engine.IO.Json;

/// <summary>
/// Tests for the <see cref="JsonByteExtractor"/> class.
/// </summary>
public sealed class JsonByteExtractorTests
{
    [Fact]
    public void ExtractNestedBytes_ForSimpleObject_ReturnsCorrectByteSlice()
    {
        // Arrange
        string json = "{\"a\": 1}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        JsonRawBytes slice = JsonByteExtractor.ExtractNestedBytes(ref reader, rawJson);

        // Assert
        slice.ToArray().Should().BeEquivalentTo(rawJson);
    }

    [Fact]
    public void ExtractNestedBytes_ForSimpleArray_ReturnsCorrectByteSlice()
    {
        // Arrange
        string json = "[1, 2]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        JsonRawBytes slice = JsonByteExtractor.ExtractNestedBytes(ref reader, rawJson);

        // Assert
        slice.ToArray().Should().BeEquivalentTo(rawJson);
    }

    [Fact]
    public void ExtractNestedBytes_ForComplexNestedStructure_ReturnsCorrectByteSlice()
    {
        // Arrange
        string json = "{\"a\": [1, 2], \"b\": {\"c\": 3}}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        JsonRawBytes slice = JsonByteExtractor.ExtractNestedBytes(ref reader, rawJson);

        // Assert
        slice.ToArray().Should().BeEquivalentTo(rawJson);
    }

    [Fact]
    public void ExtractNestedBytes_WithLargerContext_ReturnsOnlyTargetStructure()
    {
        // Arrange
        string fullJson = "{\"root\": {\"id\": 1, \"value\": \"test\"}, \"other\": 2}";
        byte[] rawJson = Encoding.UTF8.GetBytes(fullJson);
        Utf8JsonReader reader = new(rawJson);

        reader.Read(); // StartObject (root)
        reader.Read(); // PropertyName "root"
        reader.Read(); // StartObject (target nested object)

        string expectedNestedJson = "{\"id\": 1, \"value\": \"test\"}";
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedNestedJson);

        // Act
        JsonRawBytes slice = JsonByteExtractor.ExtractNestedBytes(ref reader, rawJson);

        // Assert
        slice.ToArray().Should().BeEquivalentTo(expectedBytes);
    }

    [Theory]
    [InlineData("{}", 0)]
    [InlineData("{\"a\": 1}", 1)]
    [InlineData("{\"a\": 1, \"b\": 2, \"c\": 3}", 3)]
    public void CountObjectProperties_VariousObjects_ReturnsTopLevelPropertyCount(string json, int expectedCount)
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        int propertyCount = JsonByteExtractor.CountObjectProperties(ref reader);

        // Assert
        propertyCount.Should().Be(expectedCount);
    }

    [Fact]
    public void CountObjectProperties_WithNestedObjectAndArrayValues_CountsOnlyTopLevelKeys()
    {
        // Arrange
        string json = "{\"a\": {\"nested1\": 1, \"nested2\": 2}, \"b\": [1, 2, 3]}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        int propertyCount = JsonByteExtractor.CountObjectProperties(ref reader);

        // Assert
        propertyCount.Should().Be(2);
    }

    [Fact]
    public void CountObjectProperties_AfterCounting_ReaderStopsAtMatchingEndObjectToken()
    {
        // Arrange
        string json = "[{\"a\": 1, \"b\": {\"nested\": 1}}, \"sibling\"]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartArray
        reader.Read(); // Move to StartObject (target)

        // Act
        JsonByteExtractor.CountObjectProperties(ref reader);

        // Assert
        reader.TokenType.Should().Be(JsonTokenType.EndObject);
        reader.Read().Should().BeTrue();
        reader.TokenType.Should().Be(JsonTokenType.String);
        reader.GetString().Should().Be("sibling");
    }

    [Theory]
    [InlineData("[]", 0)]
    [InlineData("[1]", 1)]
    [InlineData("[1, 2, 3]", 3)]
    public void CountArrayElements_VariousArrays_ReturnsTopLevelElementCount(string json, int expectedCount)
    {
        // Arrange
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        int elementCount = JsonByteExtractor.CountArrayElements(ref reader);

        // Assert
        elementCount.Should().Be(expectedCount);
    }

    [Fact]
    public void CountArrayElements_WithNestedObjectAndArrayElements_CountsOnlyTopLevelElements()
    {
        // Arrange
        string json = "[1, {\"x\": 1, \"y\": 2}, [1, 2, 3], 4]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        int elementCount = JsonByteExtractor.CountArrayElements(ref reader);

        // Assert
        elementCount.Should().Be(4);
    }

    [Fact]
    public void CountArrayElements_AfterCounting_ReaderStopsAtMatchingEndArrayToken()
    {
        // Arrange
        string json = "[[1, [2, 3]], \"sibling\"]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartArray (outer)
        reader.Read(); // Move to StartArray (target)

        // Act
        JsonByteExtractor.CountArrayElements(ref reader);

        // Assert
        reader.TokenType.Should().Be(JsonTokenType.EndArray);
        reader.Read().Should().BeTrue();
        reader.TokenType.Should().Be(JsonTokenType.String);
        reader.GetString().Should().Be("sibling");
    }

    [Fact]
    public void FormatObjectPreview_ForObjectWithProperties_ReturnsCollapsedPreviewString()
    {
        // Arrange
        string json = "{\"a\": 1, \"b\": 2}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        string preview = JsonByteExtractor.FormatObjectPreview(ref reader);

        // Assert
        preview.Should().Be("{Object: 2 properties}");
    }

    [Fact]
    public void FormatArrayPreview_ForArrayWithElements_ReturnsCollapsedPreviewString()
    {
        // Arrange
        string json = "[1, 2, 3]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        string preview = JsonByteExtractor.FormatArrayPreview(ref reader);

        // Assert
        preview.Should().Be("[Array: 3 items]");
    }

    [Fact]
    public void FormatObjectPreview_ForEmptyObject_ReturnsZeroPropertiesPreview()
    {
        // Arrange
        string json = "{}";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartObject

        // Act
        string preview = JsonByteExtractor.FormatObjectPreview(ref reader);

        // Assert
        preview.Should().Be("{Object: 0 properties}");
    }

    [Fact]
    public void FormatArrayPreview_ForEmptyArray_ReturnsZeroItemsPreview()
    {
        // Arrange
        string json = "[]";
        byte[] rawJson = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(rawJson);
        reader.Read(); // Move to StartArray

        // Act
        string preview = JsonByteExtractor.FormatArrayPreview(ref reader);

        // Assert
        preview.Should().Be("[Array: 0 items]");
    }
}

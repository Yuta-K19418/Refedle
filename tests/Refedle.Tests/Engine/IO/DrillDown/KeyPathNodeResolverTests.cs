using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.Tests.Engine.IO.DrillDown;

public sealed class KeyPathNodeResolverTests
{
    private static KeyPathSegment Key(string value) => new(value, KeyPathSegmentKind.Key);

    private static KeyPathSegment Index(string value) => new(value, KeyPathSegmentKind.Index);

    private static JsonRawBytes Bytes(string json) => Encoding.UTF8.GetBytes(json);

    private static JsonElement Parse(JsonRawBytes bytes)
    {
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void ResolveSingleNode_EmptyRemainingKeyPath_ReturnsStartBytesUnchanged()
    {
        // Arrange
        var bytes = Bytes("""{"name":"Alice"}""");

        // Act
        var result = KeyPathNodeResolver.ResolveSingleNode(bytes, []);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Span.SequenceEqual(bytes.Span).Should().BeTrue();
    }

    [Fact]
    public void ResolveSingleNode_KeyOnlySegments_DescendsViaNestedObjectLookups()
    {
        // Arrange
        var bytes = Bytes("""{"customer":{"name":"Acme Corp","address":{"city":"Metropolis"}}}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("customer"), Key("address"), Key("city")];

        // Act
        var result = KeyPathNodeResolver.ResolveSingleNode(bytes, keyPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Parse(result.Value).GetString().Should().Be("Metropolis");
    }

    [Fact]
    public void ResolveSingleNode_KeyThenIndexSegment_SelectsSpecificArrayElementOnly()
    {
        // Arrange
        var bytes = Bytes("""{"orders":[{"id":"A1"},{"id":"A2"},{"id":"A3"}]}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("orders"), Index("[1]")];

        // Act
        var result = KeyPathNodeResolver.ResolveSingleNode(bytes, keyPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Parse(result.Value).GetProperty("id").GetString().Should().Be("A2");
    }

    [Fact]
    public void ResolveSingleNode_MissingKey_ReturnsFailure()
    {
        // Arrange
        var bytes = Bytes("""{"customer":"Acme Corp"}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("missing")];

        // Act
        var result = KeyPathNodeResolver.ResolveSingleNode(bytes, keyPath);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ResolveSingleNode_IndexSegmentAgainstNonArray_ReturnsFailure()
    {
        // Arrange
        var bytes = Bytes("""{"tags":"not an array"}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("tags"), Index("[0]")];

        // Act
        var result = KeyPathNodeResolver.ResolveSingleNode(bytes, keyPath);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ResolveSingleNode_IndexOutOfRange_ReturnsFailure()
    {
        // Arrange
        var bytes = Bytes("""{"orders":[{"id":"A1"}]}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("orders"), Index("[5]")];

        // Act
        var result = KeyPathNodeResolver.ResolveSingleNode(bytes, keyPath);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("[0")]
    [InlineData("0]")]
    [InlineData("0")]
    [InlineData("[]")]
    [InlineData("[")]
    [InlineData("]")]
    [InlineData("")]
    public void ResolveSingleNode_MalformedIndexLabel_ReturnsFailureWithoutThrowing(string malformedIndexLabel)
    {
        // Arrange — a recipe is user-editable YAML, so a malformed "[N]" label must fail cleanly
        // instead of slicing out of bounds or being misparsed as a valid index.
        var bytes = Bytes("""{"orders":[{"id":"A1"}]}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("orders"), Index(malformedIndexLabel)];

        // Act
        Func<Result<JsonRawBytes>> act = () => KeyPathNodeResolver.ResolveSingleNode(bytes, keyPath);

        // Assert
        act.Should().NotThrow();
        act().IsFailure.Should().BeTrue();
    }
}

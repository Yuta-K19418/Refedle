using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.DrillDown;

public sealed class KeyPathTraverserTests
{
    private static KeyPathSegment Key(string value) => new(value, KeyPathSegmentKind.Key);

    private static KeyPathSegment Index(string value) => new(value, KeyPathSegmentKind.Index);

    private static JsonRawBytes Bytes(string json) => Encoding.UTF8.GetBytes(json);

    private static JsonElement GetProperty(JsonRawBytes bytes, string propertyName)
    {
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.GetProperty(propertyName).Clone();
    }

    // Builds {"k0":{"k1":...{"k{depth-1}":"leafValue"}...}} — `depth` single-key objects,
    // exercising key-segment descent to a primitive leaf.
    private static string BuildNestedObjects(int depth, string leafValue)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < depth; i++)
        {
            sb.Append("{\"k").Append(i).Append("\":");
        }

        sb.Append('"').Append(leafValue).Append('"');
        sb.Append('}', depth);
        return sb.ToString();
    }

    // Builds [[["leafValue"]]] — `depth` single-element arrays; each "[0]" descends one level.
    private static string BuildNestedArrays(int depth, string leafValue)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < depth; i++)
        {
            sb.Append('[');
        }

        sb.Append('"').Append(leafValue).Append('"');
        for (var i = 0; i < depth; i++)
        {
            sb.Append(']');
        }

        return sb.ToString();
    }

    private static List<KeyPathSegment> BuildKeySegments(int depth)
    {
        List<KeyPathSegment> segments = [];
        for (var i = 0; i < depth; i++)
        {
            segments.Add(Key($"k{i}"));
        }

        return segments;
    }

    private static List<KeyPathSegment> BuildIndexSegments(int depth)
    {
        List<KeyPathSegment> segments = [];
        for (var i = 0; i < depth; i++)
        {
            segments.Add(Index("[0]"));
        }

        return segments;
    }

    private static TraverseResult Traverse(
        JsonRawBytes recordBytes, IReadOnlyList<KeyPathSegment> keyPath, string posHash = "1")
    {
        var colName = KeyPathTraverser.LastKeySegment(keyPath);
        var colNameUtf8 = Encoding.UTF8.GetBytes(colName);
        List<FocusedTableRow> rows = [];
        List<string> keyOrder = [];
        var keySet = new HashSet<string>(StringComparer.Ordinal);
        var columnTypes = new Dictionary<string, ColumnType>(StringComparer.Ordinal);
        var keyObservedCount = new Dictionary<string, int>(StringComparer.Ordinal);

        KeyPathTraverser.ExtractRows(
            recordBytes, keyPath, posHash, colName, colNameUtf8,
            rows, keyOrder, keySet, columnTypes, keyObservedCount);

        return new TraverseResult(rows, keyOrder, columnTypes, keyObservedCount);
    }

    private readonly record struct TraverseResult(
        List<FocusedTableRow> Rows,
        List<string> KeyOrder,
        Dictionary<string, ColumnType> ColumnTypes,
        Dictionary<string, int> KeyObservedCount);

    [Fact]
    public void ExtractRows_KeyThenIndexThenKeySegments_ReachesNestedLeaves()
    {
        // Arrange
        var bytes = Bytes("""{"orders":[{"id":"A1"},{"id":"A2"}]}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("orders"), Index("[0]"), Key("id")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().HaveCount(2);
        result.Rows.Select(r => r.HashValue).Should().Equal("1:0", "1:1");
        result.Rows.Select(r => GetProperty(r.Bytes, "id").GetString()).Should().Equal("A1", "A2");
        result.KeyOrder.Should().Equal("id");
    }

    [Fact]
    public void ExtractRows_IndexSegmentLabelDoesNotMatchPosition_AggregatesAllElements()
    {
        // Arrange
        var bytes = Bytes("""{"orders":[{"id":"A1"},{"id":"A2"}]}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("orders"), Index("[99]"), Key("id")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().HaveCount(2);
        result.Rows.Select(r => r.HashValue).Should().Equal("1:0", "1:1");
        result.Rows.Select(r => GetProperty(r.Bytes, "id").GetString()).Should().Equal("A1", "A2");
    }

    [Fact]
    public void ExtractRows_KeySegmentAbsentInObject_SkipsRecordSilently()
    {
        // Arrange
        var bytes = Bytes("""{"other":"x"}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("user")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().BeEmpty();
    }

    [Fact]
    public void ExtractRows_KeySegmentWhenCurrentValueIsNotObject_SkipsRecordSilently()
    {
        // Arrange
        var bytes = Bytes("""{"user":"just a string"}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("user"), Key("name")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().BeEmpty();
    }

    [Fact]
    public void ExtractRows_IndexSegmentWhenCurrentValueIsNotArray_SkipsRecordSilently()
    {
        // Arrange
        var bytes = Bytes("""{"tags":"not an array"}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("tags"), Index("[0]")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().BeEmpty();
    }

    [Fact]
    public void ExtractRows_LeafIsObject_AddsRowAndScansSchema()
    {
        // Arrange
        var bytes = Bytes("""{"user":{"name":"Alice","age":30}}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("user")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().HaveCount(1);
        result.Rows[0].HashValue.Should().Be("1");
        GetProperty(result.Rows[0].Bytes, "name").GetString().Should().Be("Alice");
        GetProperty(result.Rows[0].Bytes, "age").GetInt32().Should().Be(30);
        result.KeyOrder.Should().Equal("name", "age");
    }

    [Fact]
    public void ExtractRows_LeafIsArray_CollectsRowPerArrayElement()
    {
        // Arrange
        var bytes = Bytes("""{"orders":[{"id":"A1"},{"id":"A2"}]}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("orders")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().HaveCount(2);
        result.Rows.Select(r => r.HashValue).Should().Equal("1:0", "1:1");
        result.Rows.Select(r => GetProperty(r.Bytes, "id").GetString()).Should().Equal("A1", "A2");
    }

    [Fact]
    public void ExtractRows_LeafIsEmptyArray_ProducesNoRows()
    {
        // Arrange
        var bytes = Bytes("""{"tags":[]}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("tags")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().BeEmpty();
        result.KeyOrder.Should().BeEmpty();
    }

    [Fact]
    public void ExtractRows_LeafIsPrimitiveString_SynthesizesObjectUsingColName()
    {
        // Arrange
        var bytes = Bytes("""{"grade":"A+"}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("grade")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().HaveCount(1);
        result.Rows[0].HashValue.Should().Be("1");
        GetProperty(result.Rows[0].Bytes, "grade").GetString().Should().Be("A+");
        result.KeyOrder.Should().Equal("grade");
    }

    [Fact]
    public void ExtractRows_LeafIsPrimitiveNull_SynthesizesObjectWithNullValue()
    {
        // Arrange
        var bytes = Bytes("""{"score":null}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("score")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().HaveCount(1);
        GetProperty(result.Rows[0].Bytes, "score").ValueKind.Should().Be(JsonValueKind.Null);
        result.KeyOrder.Should().Equal("score");
    }

    [Fact]
    public void ExtractRows_KeySegmentArrayLeafObjectElement_AddsRowAndScansElementSchema()
    {
        // Arrange
        var bytes = Bytes("""{"items":[{"id":"A1","qty":2},{"id":"A2"}]}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("items")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().HaveCount(2);
        result.KeyOrder.Should().Equal("id", "qty");
        result.KeyObservedCount["id"].Should().Be(2);
        result.KeyObservedCount["qty"].Should().Be(1);
        result.Rows.Select(r => GetProperty(r.Bytes, "id").GetString()).Should().Equal("A1", "A2");
    }

    [Fact]
    public void ExtractRows_KeySegmentArrayLeafPrimitiveElement_SynthesizesValueColumn()
    {
        // Arrange
        var bytes = Bytes("""{"tags":["dev","ops"]}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("tags")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().HaveCount(2);
        result.Rows.Select(r => r.HashValue).Should().Equal("1:0", "1:1");
        result.Rows.Select(r => GetProperty(r.Bytes, "value").GetString()).Should().Equal("dev", "ops");
        result.KeyOrder.Should().Equal("value");
    }

    [Fact]
    public void ExtractRows_KeySegmentArrayLeafMixedElements_ProducesRowPerElementKind()
    {
        // Arrange
        var bytes = Bytes("""{"items":[{"id":"A1"},"loose-string"]}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("items")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().HaveCount(2);
        GetProperty(result.Rows[0].Bytes, "id").GetString().Should().Be("A1");
        GetProperty(result.Rows[1].Bytes, "value").GetString().Should().Be("loose-string");
        result.KeyOrder.Should().Contain(["id", "value"]);
    }

    [Fact]
    public void ExtractRows_PathEndingInIndexSegment_ProducesSameRowsAsPathWithoutIt()
    {
        // Arrange
        var bytes = Bytes("""{"tags":["dev","ops"]}""");
        IReadOnlyList<KeyPathSegment> pathWithoutIndex = [Key("tags")];
        IReadOnlyList<KeyPathSegment> pathWithIndex = [Key("tags"), Index("[0]")];

        // Act
        var resultWithoutIndex = Traverse(bytes, pathWithoutIndex);
        var resultWithIndex = Traverse(bytes, pathWithIndex);

        // Assert
        resultWithoutIndex.Rows.Select(r => r.HashValue)
            .Should().Equal(resultWithIndex.Rows.Select(r => r.HashValue));
        resultWithoutIndex.Rows.Select(r => GetProperty(r.Bytes, "value").GetString())
            .Should().Equal(resultWithIndex.Rows.Select(r => GetProperty(r.Bytes, "value").GetString()));
        resultWithoutIndex.KeyOrder.Should().Equal(resultWithIndex.KeyOrder);
    }

    [Fact]
    public void ExtractRows_DeeplyNestedKeySegments_ReachesLeafWithExpectedAggregate()
    {
        // Arrange — depth 25 stays under Utf8JsonReader's default MaxDepth (64); the recursive
        // implementation must reach the leaf. Pins behavior the iterative rewrite must preserve.
        const int depth = 25;
        var bytes = Bytes(BuildNestedObjects(depth, "leafKey"));
        var keyPath = BuildKeySegments(depth);

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().HaveCount(1);
        result.Rows[0].HashValue.Should().Be("1");
        GetProperty(result.Rows[0].Bytes, $"k{depth - 1}").GetString().Should().Be("leafKey");
        result.KeyOrder.Should().Equal($"k{depth - 1}");
        result.KeyObservedCount[$"k{depth - 1}"].Should().Be(1);
    }

    [Fact]
    public void ExtractRows_DeeplyNestedIndexSegments_ReachesLeafWithExpectedAggregate()
    {
        // Arrange — each "[0]" descends one array level; the leaf hash accumulates ":0" per
        // segment. The iterative rewrite must reproduce this hash and aggregation exactly.
        const int depth = 25;
        var expectedLeafHash = "1" + string.Concat(Enumerable.Repeat(":0", depth));
        var bytes = Bytes(BuildNestedArrays(depth, "leafIndex"));
        var keyPath = BuildIndexSegments(depth);

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().HaveCount(1);
        result.Rows[0].HashValue.Should().Be(expectedLeafHash);
        GetProperty(result.Rows[0].Bytes, "value").GetString().Should().Be("leafIndex");
        result.KeyOrder.Should().Equal("value");
        result.KeyObservedCount["value"].Should().Be(1);
    }

    [Fact]
    public void ExtractRows_BranchingIndexSegments_VisitsLeavesInForwardDfsOrder()
    {
        // Arrange — orders[*].items[*].id: two index levels, each with more than one sibling,
        // pins the iterative DFS to fully visit element 0's subtree before element 1's.
        var bytes = Bytes("""{"orders":[{"items":[{"id":"a1"},{"id":"a2"}]},{"items":[{"id":"b1"}]}]}""");
        IReadOnlyList<KeyPathSegment> keyPath = [Key("orders"), Index("[0]"), Key("items"), Index("[0]"), Key("id")];

        // Act
        var result = Traverse(bytes, keyPath);

        // Assert
        result.Rows.Should().HaveCount(3);
        result.Rows.Select(r => r.HashValue).Should().Equal("1:0:0", "1:0:1", "1:1:0");
        result.Rows.Select(r => GetProperty(r.Bytes, "id").GetString()).Should().Equal("a1", "a2", "b1");
        result.KeyOrder.Should().Equal("id");
    }

    [Fact]
    public void LastKeySegment_PathEndingInKeySegment_ReturnsThatSegmentValue()
    {
        // Arrange
        IReadOnlyList<KeyPathSegment> keyPath = [Key("user"), Key("name")];

        // Act
        var result = KeyPathTraverser.LastKeySegment(keyPath);

        // Assert
        result.Should().Be("name");
    }

    [Fact]
    public void LastKeySegment_PathEndingInIndexSegment_ReturnsPrecedingKeySegment()
    {
        // Arrange
        IReadOnlyList<KeyPathSegment> keyPath = [Key("tags"), Index("[0]")];

        // Act
        var result = KeyPathTraverser.LastKeySegment(keyPath);

        // Assert
        result.Should().Be("tags");
    }

    [Fact]
    public void LastKeySegment_PathOfOnlyIndexSegments_ReturnsValueFallback()
    {
        // Arrange
        IReadOnlyList<KeyPathSegment> keyPath = [Index("[0]"), Index("[1]")];

        // Act
        var result = KeyPathTraverser.LastKeySegment(keyPath);

        // Assert
        result.Should().Be("value");
    }

    [Fact]
    public void LastKeySegment_EmptyPath_ReturnsValueFallback()
    {
        // Arrange
        IReadOnlyList<KeyPathSegment> keyPath = [];

        // Act
        var result = KeyPathTraverser.LastKeySegment(keyPath);

        // Assert
        result.Should().Be("value");
    }
}

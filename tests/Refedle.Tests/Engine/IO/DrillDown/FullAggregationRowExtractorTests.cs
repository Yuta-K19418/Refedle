using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.Json;

namespace Refedle.Tests.Engine.IO.DrillDown;

public sealed class FullAggregationRowExtractorTests
{
    [Fact]
    public void ExtractRows_ObjectLeaf_ReturnsOneRowPerRecord()
    {
        // Arrange
        IReadOnlyList<JsonRawBytes> batch =
        [
            Bytes("""{"orders":[{"id":1}]}"""),
            Bytes("""{"orders":[{"id":2}]}"""),
        ];

        // Act
        var rows = FullAggregationRowExtractor.ExtractRows(batch, KeyPath("orders"));

        // Assert
        rows.Select(r => Id(r.Bytes)).Should().Equal([1, 2]);
    }

    [Fact]
    public void ExtractRows_PathAbsentInRecord_ContributesNoRows()
    {
        // Arrange — the middle record lacks "orders" entirely.
        IReadOnlyList<JsonRawBytes> batch =
        [
            Bytes("""{"orders":[{"id":1}]}"""),
            Bytes("""{"other":true}"""),
            Bytes("""{"orders":[{"id":3}]}"""),
        ];

        // Act
        var rows = FullAggregationRowExtractor.ExtractRows(batch, KeyPath("orders"));

        // Assert
        rows.Select(r => Id(r.Bytes)).Should().Equal([1, 3]);
    }

    [Fact]
    public void ExtractRows_ArrayLeaf_ExpandsToOneRowPerElement()
    {
        // Arrange
        IReadOnlyList<JsonRawBytes> batch = [Bytes("""{"orders":[{"id":1},{"id":2},{"id":3}]}""")];

        // Act
        var rows = FullAggregationRowExtractor.ExtractRows(batch, KeyPath("orders"));

        // Assert
        rows.Select(r => Id(r.Bytes)).Should().Equal([1, 2, 3]);
    }

    [Fact]
    public void ExtractRows_PrimitiveArrayLeaf_SynthesizesRetrievableValueRows()
    {
        // Arrange — primitive elements take the synthesized {"value": ...} branch.
        IReadOnlyList<JsonRawBytes> batch = [Bytes("""{"tags":["x","y"]}""")];

        // Act
        var rows = FullAggregationRowExtractor.ExtractRows(batch, KeyPath("tags"));

        // Assert
        rows.Select(r => JsonObjectCellExtractor.ExtractCell(r.Bytes.Span, "value"u8)).Should().Equal(["x", "y"]);
    }

    [Fact]
    public void ExtractRows_EmptyKeyPath_TreatsEachRecordAsTheRow()
    {
        // Arrange
        IReadOnlyList<JsonRawBytes> batch = [Bytes("""{"id":7}"""), Bytes("""{"id":8}""")];

        // Act
        var rows = FullAggregationRowExtractor.ExtractRows(batch, KeyPath());

        // Assert
        rows.Select(r => Id(r.Bytes)).Should().Equal([7, 8]);
    }

    [Fact]
    public void ExtractRows_EmptyBatch_ReturnsNoRows()
    {
        // Arrange
        IReadOnlyList<JsonRawBytes> batch = [];

        // Act
        var rows = FullAggregationRowExtractor.ExtractRows(batch, KeyPath("orders"));

        // Assert
        rows.Should().BeEmpty();
    }

    [Fact]
    public void ExtractRows_NullBatch_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<JsonRawBytes> batch = null!;

        // Act
        var act = () => FullAggregationRowExtractor.ExtractRows(batch, KeyPath("orders"));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExtractRows_NullKeyPath_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<KeyPathSegment> keyPath = null!;

        // Act
        var act = () => FullAggregationRowExtractor.ExtractRows([], keyPath);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static IReadOnlyList<KeyPathSegment> KeyPath(params string[] segments)
        => [.. segments.Select(static s => new KeyPathSegment(s, KeyPathSegmentKind.Key))];

    private static JsonRawBytes Bytes(string json) => Encoding.UTF8.GetBytes(json);

    private static int Id(JsonRawBytes bytes)
    {
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.GetProperty("id").GetInt32();
    }
}

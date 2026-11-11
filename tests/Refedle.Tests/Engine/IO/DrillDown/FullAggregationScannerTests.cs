using System.Globalization;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.Json;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.DrillDown;

public sealed class FullAggregationScannerTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private string CreateTempFile(DataFormat format, params string[] records) =>
        WriteTempFile(format, withUtf8Bom: false, records);

    private string CreateTempFileWithUtf8Bom(DataFormat format, params string[] records) =>
        WriteTempFile(format, withUtf8Bom: true, records);

    private string WriteTempFile(DataFormat format, bool withUtf8Bom, string[] records)
    {
        var extension = format == DataFormat.JsonLines ? ".jsonl" : ".json";
        var path = Path.ChangeExtension(Path.GetTempFileName(), extension);
        var content = format == DataFormat.JsonArray
            ? $"[{string.Join(",", records)}]"
            : string.Join("\n", records);
        var bytes = withUtf8Bom
            ? [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(content)]
            : Encoding.UTF8.GetBytes(content);
        File.WriteAllBytes(path, bytes);

        _tempFiles.Add(path);
        return path;
    }

    private static string Pos(DataFormat format, int recordIndex) =>
        (format == DataFormat.JsonLines ? recordIndex + 1 : recordIndex).ToString(CultureInfo.InvariantCulture);

    private static JsonElement GetProperty(JsonRawBytes bytes, string propertyName)
    {
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.GetProperty(propertyName).Clone();
    }

    /// <summary>
    /// Test-only shorthand: builds a KeyPath tagging "[n]"-shaped segments as Index and everything else as Key. Production BuildKeyPath tags by parent node type instead, not label text.
    /// </summary>
    private static IReadOnlyList<KeyPathSegment> KeyPath(params string[] segments)
        => [.. segments.Select(static s => new KeyPathSegment(
            s,
            s.StartsWith('[', StringComparison.Ordinal) ? KeyPathSegmentKind.Index : KeyPathSegmentKind.Key))];

    [Fact]
    public void Scan_WithUtf8BomPrefixedJsonArray_ReturnsOneRowPerRecord()
    {
        // Arrange — the ScanElements path must skip the BOM like the JSON Lines path does.
        var path = CreateTempFileWithUtf8Bom(
            DataFormat.JsonArray,
            """{"user":{"name":"Alice","age":30}}""",
            """{"user":{"name":"Bob","age":25}}""");

        // Act
        var result = FullAggregationScanner.Scan(path, DataFormat.JsonArray, KeyPath("user"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(2);
        result.Value.rows[0].HashValue.Should().Be("0");
        result.Value.rows[1].HashValue.Should().Be("1");
        GetProperty(result.Value.rows[0].Bytes, "name").GetString().Should().Be("Alice");
        GetProperty(result.Value.rows[1].Bytes, "name").GetString().Should().Be("Bob");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_ObjectLeafInAllRecords_ReturnsOneRowPerRecord(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(
            format,
            """{"user":{"name":"Alice","age":30}}""",
            """{"user":{"name":"Bob","age":25}}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("user"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(2);
        result.Value.rows[0].HashValue.Should().Be(Pos(format, 0));
        result.Value.rows[1].HashValue.Should().Be(Pos(format, 1));
        GetProperty(result.Value.rows[0].Bytes, "name").GetString().Should().Be("Alice");
        GetProperty(result.Value.rows[1].Bytes, "name").GetString().Should().Be("Bob");
        result.Value.schema.Columns.Select(c => c.Name).Should().Equal("name", "age");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_ArrayOfObjectLeafInAllRecords_ReturnsOneRowPerElement(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(
            format,
            """{"orders":[{"id":"A1","qty":2},{"id":"A2","qty":5}]}""",
            """{"orders":[{"id":"B1","qty":1}]}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("orders"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(3);
        result.Value.rows.Select(r => r.HashValue).Should().Equal(
            $"{Pos(format, 0)}:0", $"{Pos(format, 0)}:1", $"{Pos(format, 1)}:0");
        result.Value.rows.Select(r => GetProperty(r.Bytes, "id").GetString())
            .Should().Equal("A1", "A2", "B1");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_ArrayOfPrimitiveLeafInAllRecords_ReturnsRowsWithValueColumn(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(
            format,
            """{"tags":["dev","ops"]}""",
            """{"tags":["dev"]}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("tags"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(3);
        result.Value.rows.Select(r => r.HashValue).Should().Equal(
            $"{Pos(format, 0)}:0", $"{Pos(format, 0)}:1", $"{Pos(format, 1)}:0");
        result.Value.rows.Select(r => GetProperty(r.Bytes, "value").GetString())
            .Should().Equal("dev", "ops", "dev");
        result.Value.schema.Columns.Select(c => c.Name).Should().Equal("value");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_PrimitiveLeaf_ReturnsOneRowPerRecordWithKeyNamedColumn(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(
            format,
            """{"score":88}""",
            """{"score":72}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("score"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(2);
        result.Value.rows[0].HashValue.Should().Be(Pos(format, 0));
        result.Value.rows[1].HashValue.Should().Be(Pos(format, 1));
        GetProperty(result.Value.rows[0].Bytes, "score").GetInt32().Should().Be(88);
        GetProperty(result.Value.rows[1].Bytes, "score").GetInt32().Should().Be(72);
        result.Value.schema.Columns.Select(c => c.Name).Should().Equal("score");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_IndexSegmentInPath_ProducesSameOutputAsParentArray(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(format, """{"orders":[{"id":"A1"},{"id":"A2"}]}""");

        // Act
        var resultWithoutIndex = FullAggregationScanner.Scan(path, format, KeyPath("orders"));
        var resultWithIndex = FullAggregationScanner.Scan(path, format, KeyPath("orders", "[0]"));

        // Assert
        resultWithoutIndex.IsSuccess.Should().BeTrue();
        resultWithIndex.IsSuccess.Should().BeTrue();
        resultWithoutIndex.Value.rows.Select(r => r.HashValue)
            .Should().Equal(resultWithIndex.Value.rows.Select(r => r.HashValue));
        resultWithoutIndex.Value.rows.Select(r => GetProperty(r.Bytes, "id").GetString())
            .Should().Equal(resultWithIndex.Value.rows.Select(r => GetProperty(r.Bytes, "id").GetString()));
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_TwoIndexSegmentsInPath_HashHasTwoColonSeparators(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(
            format,
            """{"orders":[{"tags":["urgent","gift"]},{"tags":["normal"]}]}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("orders", "[0]", "tags"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(3);
        result.Value.rows.Select(r => r.HashValue).Should().Equal(
            $"{Pos(format, 0)}:0:0", $"{Pos(format, 0)}:0:1", $"{Pos(format, 0)}:1:0");
        result.Value.rows.Select(r => GetProperty(r.Bytes, "value").GetString())
            .Should().Equal("urgent", "gift", "normal");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_KeyMissingInSomeRecords_SkipsThoseRecordsSilently(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(
            format,
            """{"user":{"name":"Alice"}}""",
            """{"other":"x"}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("user"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(1);
        result.Value.rows[0].HashValue.Should().Be(Pos(format, 0));
        GetProperty(result.Value.rows[0].Bytes, "name").GetString().Should().Be("Alice");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_KeySegmentFollowedByNonObjectToken_SkipsRecordSilently(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(
            format,
            """{"user":"just a string"}""",
            """{"user":{"name":"Bob"}}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("user", "name"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(1);
        result.Value.rows[0].HashValue.Should().Be(Pos(format, 1));
        GetProperty(result.Value.rows[0].Bytes, "name").GetString().Should().Be("Bob");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_IndexSegmentFollowedByNonArrayToken_SkipsRecordSilently(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(
            format,
            """{"tags":"not an array"}""",
            """{"tags":["x","y"]}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("tags", "[0]"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(2);
        result.Value.rows.Select(r => r.HashValue).Should().Equal(
            $"{Pos(format, 1)}:0", $"{Pos(format, 1)}:1");
        result.Value.rows.Select(r => GetProperty(r.Bytes, "value").GetString())
            .Should().Equal("x", "y");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_NoRecordsMatch_ReturnsFailureWithNoMatchingRecordsMessage(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(format, """{"user":{"name":"Alice"}}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("missing"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No matching records found.");
    }

    [Fact]
    public void Scan_JsonObjectFormat_ReturnsFailure()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "does-not-need-to-exist.json");

        // Act
        var result = FullAggregationScanner.Scan(path, DataFormat.JsonObject, KeyPath("user"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("JSON Object format does not support full aggregation.");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_AllMatchedLeafObjectsEmpty_ReturnsFailureWithNoKeysMessage(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(
            format,
            """{"user":{}}""",
            """{"user":{}}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("user"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("All child objects have no keys");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_VaryingKeysAcrossRecords_BuildsUnionSchemaWithNullableForMissingKeys(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(
            format,
            """{"user":{"name":"Alice","age":30}}""",
            """{"user":{"name":"Bob"}}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("user"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(2);
        result.Value.schema.GetColumn("name").Should().BeOfType<ColumnSchema>()
            .Which.IsNullable.Should().BeFalse();
        result.Value.schema.GetColumn("age").Should().BeOfType<ColumnSchema>()
            .Which.IsNullable.Should().BeTrue();
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_CancellationRequestedBeforeScanStarts_ThrowsOperationCanceledException(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(format, """{"user":{"name":"Alice"}}""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => FullAggregationScanner.Scan(path, format, KeyPath("user"), cts.Token);

        // Assert
        act.Should().Throw<OperationCanceledException>();
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_NestedObjectOrArrayCellValue_RendersAsCollapsedPreview(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(
            format,
            """{"user":{"name":"Alice","address":{"city":"Tokyo"},"tags":["dev","ops"]}}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("user"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(1);
        JsonObjectCellExtractor.ExtractCell(result.Value.rows[0].Bytes.Span, "address"u8).Should().Be("{Object: 1 properties}");
        JsonObjectCellExtractor.ExtractCell(result.Value.rows[0].Bytes.Span, "tags"u8).Should().Be("[Array: 2 items]");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_LeafValueIsNull_EmitsOneNullRow(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(format, """{"score":null}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("score"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(1);
        JsonObjectCellExtractor.ExtractCell(result.Value.rows[0].Bytes.Span, "score"u8).Should().Be("<null>");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_EmptyArrayAtLeafPosition_ContributesZeroRowsForThatRecord(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(
            format,
            """{"tags":[]}""",
            """{"tags":["x"]}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("tags"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(1);
        result.Value.rows[0].HashValue.Should().Be($"{Pos(format, 1)}:0");
        GetProperty(result.Value.rows[0].Bytes, "value").GetString().Should().Be("x");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_MixedArrayOfObjectAndPrimitiveElements_ProducesCorrespondingRowTypes(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(format, """{"items":[{"id":"A1"},"loose-string"]}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("items"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(2);
        GetProperty(result.Value.rows[0].Bytes, "id").GetString().Should().Be("A1");
        GetProperty(result.Value.rows[1].Bytes, "value").GetString().Should().Be("loose-string");
        result.Value.schema.Columns.Select(c => c.Name).Should().Contain(["id", "value"]);
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_Utf8MultiBytePropertyName_RoundTripsAsColumnIdentifier(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(format, """{"user":{"名前":"Alice"}}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("user"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.schema.Columns.Select(c => c.Name).Should().Equal("名前");
        JsonObjectCellExtractor.ExtractCell(result.Value.rows[0].Bytes.Span, Encoding.UTF8.GetBytes("名前"))
            .Should().Be("Alice");
    }

    [Fact]
    public void Scan_JsonLinesWithEmptyLineInMiddle_PreservesLineBasedRecordPosition()
    {
        // Arrange
        var path = CreateTempFile(DataFormat.JsonLines, """{"score":1}""", "", """{"score":3}""");

        // Act
        var result = FullAggregationScanner.Scan(path, DataFormat.JsonLines, KeyPath("score"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(2);
        result.Value.rows[0].HashValue.Should().Be("1");
        result.Value.rows[1].HashValue.Should().Be("3");
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_EmptyKeyPath_TreatsEachRecordAsRowWithTopLevelKeysAsColumns(DataFormat format)
    {
        // Arrange
        var path = CreateTempFile(
            format,
            """{"name":"Alice","age":30}""",
            """{"name":"Bob","age":25}""");

        // Act
        var result = FullAggregationScanner.Scan(path, format, []);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(2);
        result.Value.rows[0].HashValue.Should().Be(Pos(format, 0));
        result.Value.rows[1].HashValue.Should().Be(Pos(format, 1));
        result.Value.schema.Columns.Select(c => c.Name).Should().Equal("name", "age");
        GetProperty(result.Value.rows[0].Bytes, "name").GetString().Should().Be("Alice");
        GetProperty(result.Value.rows[1].Bytes, "name").GetString().Should().Be("Bob");
    }

    [Fact]
    public void Scan_JsonLinesFileWithUtf8Bom_SkipsBomAndParsesFirstLine()
    {
        // Arrange
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
        byte[] bom = [0xEF, 0xBB, 0xBF];
        var content = Encoding.UTF8.GetBytes("""{"score":88}""");
        File.WriteAllBytes(path, [.. bom, .. content]);
        _tempFiles.Add(path);

        // Act
        var result = FullAggregationScanner.Scan(path, DataFormat.JsonLines, KeyPath("score"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(1);
        result.Value.rows[0].HashValue.Should().Be("1");
        GetProperty(result.Value.rows[0].Bytes, "score").GetInt32().Should().Be(88);
    }

    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_ObjectKeyStartingWithBracket_TreatedAsKeyNotIndex(DataFormat format)
    {
        // Arrange — a KeyPath segment "[0]" is ambiguous: it's both a literal object key here and
        // the marker this codebase uses for array indices. That ambiguity breaks two independent
        // methods that read KeyPath segments: TraverseKeyPath misreads "[0]" as an index and
        // silently skips the record entirely (asserted via IsSuccess/row count), while
        // LastKeySegment misreads it the same way and synthesizes the wrong leaf column name
        // (asserted via the schema column name). The "[0]" segment below is built directly, not
        // via the KeyPath(...) helper, because that helper uses the same flawed '[' heuristic and
        // would silently defeat this test.
        var path = CreateTempFile(format, """{"a":{"[0]":"hello"}}""");
        IReadOnlyList<KeyPathSegment> keyPath =
        [
            new KeyPathSegment("a", KeyPathSegmentKind.Key),
            new KeyPathSegment("[0]", KeyPathSegmentKind.Key),
        ];

        // Act
        var result = FullAggregationScanner.Scan(path, format, keyPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(1);
        result.Value.schema.Columns.Select(c => c.Name).Should().Equal("[0]");
        GetProperty(result.Value.rows[0].Bytes, "[0]").GetString().Should().Be("hello");
    }

    /// <summary>
    /// Builds a file where a padding record ends just short of FileChunkReader.BufferSize and the
    /// following "note" record's value straddles that boundary, forcing a FillBuffer carry-over
    /// (JsonLines) or a multi-segment Utf8JsonReader continuation (JsonArray) mid-value.
    /// </summary>
    private (string path, string expectedValue) CreateBoundaryStraddlingFile(
        DataFormat format, bool withUtf8Bom = false)
    {
        const int remaining = 100;
        const int targetValueLength = 500;
        var targetValue = new string('b', targetValueLength);
        var target = "{\"note\":\"" + targetValue + "\"}";

        var wrapperOverhead = "{\"pad\":\"".Length + "\"}".Length;
        var extraOverhead = format == DataFormat.JsonLines ? 1 : 2; // newline, or "[" + ","
        var padLen = FileChunkReader.BufferSize - remaining - wrapperOverhead - extraOverhead;
        var padElem = "{\"pad\":\"" + new string('a', padLen) + "\"}";

        var content = format == DataFormat.JsonLines
            ? padElem + "\n" + target
            : "[" + padElem + "," + target + "]";
        var extension = format == DataFormat.JsonLines ? ".jsonl" : ".json";

        var path = Path.ChangeExtension(Path.GetTempFileName(), extension);
        var bytes = withUtf8Bom
            ? [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(content)]
            : Encoding.UTF8.GetBytes(content);
        File.WriteAllBytes(path, bytes);

        _tempFiles.Add(path);
        return (path, targetValue);
    }

    // Generates a record that straddles FileChunkReader.BufferSize exactly, to confirm ScanLines/ScanElements carry leftover bytes across FillBuffer calls without corruption.
    [Theory]
    [InlineData(DataFormat.JsonLines)]
    [InlineData(DataFormat.JsonArray)]
    public void Scan_WhenLineSpansBufferBoundary_ParsesRecordWithoutCorruption(DataFormat format)
    {
        // Arrange
        var (path, expectedValue) = CreateBoundaryStraddlingFile(format);

        // Act
        var result = FullAggregationScanner.Scan(path, format, KeyPath("note"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(1);
        GetProperty(result.Value.rows[0].Bytes, "note").GetString().Should().Be(expectedValue);
    }

    [Fact]
    public void Scan_WithUtf8BomAndJsonArrayElementSpanningBufferBoundary_ParsesRecordWithoutCorruption()
    {
        // Arrange — the boundary-straddling JSON Array fixture prefixed with a BOM: the shifted
        // start offset must not corrupt the multi-segment reader continuation mid-value.
        var (path, expectedValue) = CreateBoundaryStraddlingFile(DataFormat.JsonArray, withUtf8Bom: true);

        // Act
        var result = FullAggregationScanner.Scan(path, DataFormat.JsonArray, KeyPath("note"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(1);
        GetProperty(result.Value.rows[0].Bytes, "note").GetString().Should().Be(expectedValue);
    }
}

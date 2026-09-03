using System.Diagnostics;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.DrillDown;

public sealed class FullAggregationSchemaScannerTests : IDisposable
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

    [Fact]
    public void Scan_ObjectLeafInEveryRecord_FoldsUnionSchemaInFirstSeenOrder()
    {
        // Arrange — "note" appears only in the second record, so it must be nullable.
        var file = CreateJsonArray(
            """{"orders":[{"id":1,"item":"a"}]}""",
            """{"orders":[{"id":2,"item":"b","note":"x"}]}""");
        var keyPath = KeyPath("orders");

        // Act
        var result = FullAggregationSchemaScanner.Scan(file, DataFormat.JsonArray, keyPath, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Select(c => c.Name).Should().Equal(["id", "item", "note"]);
        result.Value.Columns.Single(c => c.Name == "note").IsNullable.Should().BeTrue();
        result.Value.Columns.Single(c => c.Name == "id").IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Scan_SpanningMultipleBatches_IncludesColumnsFromEveryBatch()
    {
        // Arrange — 1001 records span two batches (BatchSize = 1000); "late" is only in the last.
        var records = Enumerable.Range(0, 1000)
            .Select(_ => """{"orders":[{"a":1}]}""")
            .Append("""{"orders":[{"a":1,"late":2}]}""")
            .ToArray();
        var file = CreateJsonArray(records);

        // Act
        var result = FullAggregationSchemaScanner.Scan(file, DataFormat.JsonArray, KeyPath("orders"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Select(c => c.Name).Should().Equal(["a", "late"]);
    }

    [Fact]
    public void Scan_PrimitiveArrayLeaf_SynthesizesValueColumn()
    {
        // Arrange
        var file = CreateJsonArray("""{"tags":["x","y"]}""", """{"tags":["z"]}""");

        // Act
        var result = FullAggregationSchemaScanner.Scan(file, DataFormat.JsonArray, KeyPath("tags"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Select(c => c.Name).Should().Equal(["value"]);
    }

    [Fact]
    public void Scan_EmptyKeyPath_TreatsEachRecordAsTheLeaf()
    {
        // Arrange
        var file = CreateJsonArray("""{"a":1,"b":2}""", """{"a":3}""");

        // Act
        var result = FullAggregationSchemaScanner.Scan(file, DataFormat.JsonArray, KeyPath(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Select(c => c.Name).Should().Equal(["a", "b"]);
    }

    [Fact]
    public void Scan_EmptyRootArray_ReturnsNoMatchingRecordsFailure()
    {
        // Arrange — "[]" is a valid root scope, but yields zero rows and therefore no schema.
        var file = CreateJsonArray();

        // Act
        var result = FullAggregationSchemaScanner.Scan(file, DataFormat.JsonArray, KeyPath(), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No matching records found.");
    }

    [Fact]
    public void Scan_TruncatedJsonArray_ThrowsJsonException()
    {
        // Arrange — the index build tokenizes the file and rejects an unterminated root array.
        var file = CreateRawFile("""[{"id":1}""");

        // Act
        var act = () => FullAggregationSchemaScanner.Scan(file, DataFormat.JsonArray, KeyPath("orders"), CancellationToken.None);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Scan_PathAbsentInEveryRecord_ReturnsNoMatchingRecordsFailure()
    {
        // Arrange
        var file = CreateJsonArray("""{"other":1}""", """{"other":2}""");

        // Act
        var result = FullAggregationSchemaScanner.Scan(file, DataFormat.JsonArray, KeyPath("orders"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No matching records found.");
    }

    [Fact]
    public void Scan_LeafObjectsHaveNoKeys_ReturnsKeylessFailure()
    {
        // Arrange
        var file = CreateJsonArray("""{"orders":[{},{}]}""");

        // Act
        var result = FullAggregationSchemaScanner.Scan(file, DataFormat.JsonArray, KeyPath("orders"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("All child objects have no keys");
    }

    [Fact]
    public void Scan_JsonObjectFormat_ThrowsUnreachable()
    {
        // Arrange — Full Aggregation covers JSON Array and JSON Lines only.
        var file = CreateJsonArray("""{"orders":[{"id":1}]}""");

        // Act
        var act = () => FullAggregationSchemaScanner.Scan(file, DataFormat.JsonObject, KeyPath("orders"), CancellationToken.None);

        // Assert
        act.Should().Throw<UnreachableException>();
    }

    [Fact]
    public void Scan_JsonLinesObjectLeafInEveryRecord_FoldsUnionSchemaInFirstSeenOrder()
    {
        // Arrange — "note" appears only in the second line, so it must be nullable.
        var file = CreateJsonLines(
            """{"orders":[{"id":1,"item":"a"}]}""",
            """{"orders":[{"id":2,"item":"b","note":"x"}]}""");

        // Act
        var result = FullAggregationSchemaScanner.Scan(file, DataFormat.JsonLines, KeyPath("orders"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Select(c => c.Name).Should().Equal(["id", "item", "note"]);
        result.Value.Columns.Single(c => c.Name == "note").IsNullable.Should().BeTrue();
    }

    [Fact]
    public void Scan_JsonLinesSpanningMultipleBatches_IncludesColumnsFromEveryBatch()
    {
        // Arrange — 1001 lines span two batches (BatchSize = 1000); "late" is only in the last.
        var lines = Enumerable.Range(0, 1000)
            .Select(_ => """{"orders":[{"a":1}]}""")
            .Append("""{"orders":[{"a":1,"late":2}]}""")
            .ToArray();
        var file = CreateJsonLines(lines);

        // Act
        var result = FullAggregationSchemaScanner.Scan(file, DataFormat.JsonLines, KeyPath("orders"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Select(c => c.Name).Should().Equal(["a", "late"]);
    }

    [Fact]
    public void Scan_JsonLinesEmptyKeyPath_TreatsEachLineAsTheLeaf()
    {
        // Arrange
        var file = CreateJsonLines("""{"a":1,"b":2}""", """{"a":3}""");

        // Act
        var result = FullAggregationSchemaScanner.Scan(file, DataFormat.JsonLines, KeyPath(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Select(c => c.Name).Should().Equal(["a", "b"]);
    }

    [Fact]
    public void Scan_JsonLinesPathAbsentInEveryRecord_ReturnsNoMatchingRecordsFailure()
    {
        // Arrange
        var file = CreateJsonLines("""{"other":1}""", """{"other":2}""");

        // Act
        var result = FullAggregationSchemaScanner.Scan(file, DataFormat.JsonLines, KeyPath("orders"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No matching records found.");
    }

    [Fact]
    public void Scan_JsonLinesLeafObjectsHaveNoKeys_ReturnsKeylessFailure()
    {
        // Arrange
        var file = CreateJsonLines("""{"orders":[{},{}]}""");

        // Act
        var result = FullAggregationSchemaScanner.Scan(file, DataFormat.JsonLines, KeyPath("orders"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("All child objects have no keys");
    }

    [Fact]
    public void Scan_JsonLinesPrimitiveArrayLeaf_SynthesizesValueColumn()
    {
        // Arrange
        var file = CreateJsonLines("""{"tags":["x","y"]}""", """{"tags":["z"]}""");

        // Act
        var result = FullAggregationSchemaScanner.Scan(file, DataFormat.JsonLines, KeyPath("tags"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Select(c => c.Name).Should().Equal(["value"]);
    }

    [Fact]
    public void Scan_JsonLinesWithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var file = CreateJsonLines("""{"orders":[{"id":1}]}""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => FullAggregationSchemaScanner.Scan(file, DataFormat.JsonLines, KeyPath("orders"), cts.Token);

        // Assert
        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Scan_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var file = CreateJsonArray("""{"orders":[{"id":1}]}""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => FullAggregationSchemaScanner.Scan(file, DataFormat.JsonArray, KeyPath("orders"), cts.Token);

        // Assert
        act.Should().Throw<OperationCanceledException>();
    }

    private static IReadOnlyList<KeyPathSegment> KeyPath(params string[] segments)
        => [.. segments.Select(static s => new KeyPathSegment(s, KeyPathSegmentKind.Key))];

    private string CreateJsonArray(params string[] records) =>
        CreateRawFile($"[{string.Join(",", records)}]");

    private string CreateJsonLines(params string[] lines) =>
        CreateRawFile(string.Join("\n", lines) + "\n");

    private string CreateRawFile(string content)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }
}

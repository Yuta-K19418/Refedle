using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Cli;

public sealed class ColumnNameResolverTests : IDisposable
{
    private readonly string _testDir;

    public ColumnNameResolverTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveColumnNames_WithCsvInput_ReturnsHeaderColumnNames()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", "name,age\nAlice,30");

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(DataFormat.Csv, inputFile, drillDownKeyPath: null, CancellationToken.None);

        // Assert
        namesResult.IsSuccess.Should().BeTrue();
        namesResult.Value.Should().Equal(["name", "age"]);
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonLinesSpanningMultipleBatches_IncludesColumnsFromEveryBatch()
    {
        // Arrange — 1001 rows spans two batches (BatchSize = 1000); "b" only appears in the second.
        var lines = Enumerable.Range(0, 1000).Select(_ => """{"a":1}""").Append("""{"a":1,"b":2}""");
        var inputFile = CreateTestFile("input.jsonl", $"{string.Join("\n", lines)}\n");

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(DataFormat.JsonLines, inputFile, drillDownKeyPath: null, CancellationToken.None);

        // Assert
        namesResult.IsSuccess.Should().BeTrue();
        namesResult.Value.Should().Equal(["a", "b"]);
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonLinesColumnBeyondInitialScanCap_IncludesColumn()
    {
        // Arrange — "late" first appears at row 201, beyond the old 200-row initial-scan cap.
        var lines = Enumerable.Range(0, 200).Select(_ => """{"a":1}""").Append("""{"a":1,"late":2}""");
        var inputFile = CreateTestFile("input.jsonl", $"{string.Join("\n", lines)}\n");

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(DataFormat.JsonLines, inputFile, drillDownKeyPath: null, CancellationToken.None);

        // Assert
        namesResult.IsSuccess.Should().BeTrue();
        namesResult.Value.Should().Equal(["a", "late"]);
    }

    [Fact]
    public async Task ResolveColumnNames_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var inputFile = CreateTestFile("input.jsonl", """{"a":1}""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => ColumnNameResolver.ResolveColumnNamesAsync(DataFormat.JsonLines, inputFile, drillDownKeyPath: null, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ResolveColumnNames_WithUnsupportedFormat_ThrowsNotSupportedException()
    {
        // Arrange — an enum value outside the DataFormat range: every real format is now handled.
        var inputFile = CreateTestFile("input.csv", "name,age\nAlice,30");

        // Act
        var act = () => ColumnNameResolver.ResolveColumnNamesAsync((DataFormat)999, inputFile, drillDownKeyPath: null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*999*");
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonLinesAndKeyPath_ReturnsFullAggregationColumnNames()
    {
        // Arrange — the KeyPath is traversed for every line; the column set is the union of leaves.
        var lines = new[]
        {
            """{"orders":[{"id":1,"item":"a"}]}""",
            """{"orders":[{"id":2,"note":"x"}]}""",
        };
        var inputFile = CreateTestFile("input.jsonl", $"{string.Join("\n", lines)}\n");
        var keyPath = new KeyPathSegment[] { new("orders", KeyPathSegmentKind.Key) };

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
            DataFormat.JsonLines, inputFile, drillDownKeyPath: keyPath, CancellationToken.None);

        // Assert
        namesResult.IsSuccess.Should().BeTrue();
        namesResult.Value.Should().Equal(["id", "item", "note"]);
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonLinesAndKeyPathMatchingNothing_PropagatesScanFailure()
    {
        // Arrange — the DrillDown path is absent from every line.
        var inputFile = CreateTestFile("input.jsonl", "{\"other\":1}\n{\"other\":2}\n");
        var keyPath = new KeyPathSegment[] { new("orders", KeyPathSegmentKind.Key) };

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
            DataFormat.JsonLines, inputFile, drillDownKeyPath: keyPath, CancellationToken.None);

        // Assert
        namesResult.IsFailure.Should().BeTrue();
        namesResult.Error.Should().Be("No matching records found.");
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonArrayAndKeyPath_ReturnsDrilledDownColumnNames()
    {
        // Arrange — the union of every record's drilled-down child keys, in first-seen order.
        var inputFile = CreateTestFile(
            "input.json",
            """[{"orders":[{"id":1,"item":"a"},{"id":2}]},{"orders":[{"id":3,"note":"x"}]}]""");
        var keyPath = new KeyPathSegment[] { new("orders", KeyPathSegmentKind.Key) };

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
            DataFormat.JsonArray, inputFile, drillDownKeyPath: keyPath, CancellationToken.None);

        // Assert
        namesResult.IsSuccess.Should().BeTrue();
        namesResult.Value.Should().Equal(["id", "item", "note"]);
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonArrayAndNullKeyPath_ThrowsInvalidOperationException()
    {
        // Arrange — null cannot reach this branch: DrillDownRecipeValidator rejects it upstream.
        var inputFile = CreateTestFile("input.json", """[{"orders":[{"id":1}]}]""");

        // Act
        var act = () => ColumnNameResolver.ResolveColumnNamesAsync(
            DataFormat.JsonArray, inputFile, drillDownKeyPath: null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonArrayAndEmptyKeyPath_ReturnsWholeRecordColumnNames()
    {
        // Arrange — an empty path is a valid scope: each top-level record is itself the leaf.
        var inputFile = CreateTestFile("input.json", """[{"a":1,"b":2},{"a":3}]""");

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
            DataFormat.JsonArray, inputFile, drillDownKeyPath: [], CancellationToken.None);

        // Assert
        namesResult.IsSuccess.Should().BeTrue();
        namesResult.Value.Should().Equal(["a", "b"]);
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonArrayAndPathAbsentInEveryRecord_PropagatesScanFailure()
    {
        // Arrange — the path matches nothing, so the scan reports no rows.
        var inputFile = CreateTestFile("input.json", """[{"other":1},{"other":2}]""");
        var keyPath = new KeyPathSegment[] { new("orders", KeyPathSegmentKind.Key) };

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
            DataFormat.JsonArray, inputFile, drillDownKeyPath: keyPath, CancellationToken.None);

        // Assert
        namesResult.IsFailure.Should().BeTrue();
        namesResult.Error.Should().Be("No matching records found.");
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonObjectAndKeyPath_ReturnsDrilledDownColumnNames()
    {
        // Arrange — the union of the child objects' keys, in first-seen order.
        var inputFile = CreateTestFile("input.json", """{"orders":[{"id":1,"item":"a"},{"id":2}]}""");
        var keyPath = new KeyPathSegment[] { new("orders", KeyPathSegmentKind.Key) };

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
            DataFormat.JsonObject, inputFile, drillDownKeyPath: keyPath, CancellationToken.None);

        // Assert
        namesResult.IsSuccess.Should().BeTrue();
        namesResult.Value.Should().Equal(["id", "item"]);
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonObjectAndNullKeyPath_ReturnsFailure()
    {
        // Arrange — null is rejected with the TUI's empty-path message.
        var inputFile = CreateTestFile("input.json", """{"orders":[{"id":1}]}""");

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
            DataFormat.JsonObject, inputFile, drillDownKeyPath: null, CancellationToken.None);

        // Assert
        namesResult.IsFailure.Should().BeTrue();
        namesResult.Error.Should().Be("This recipe's DrillDown path is empty, which is not valid for a JSON Object file.");
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonObjectAndEmptyKeyPath_ReturnsFailure()
    {
        // Arrange — present-but-empty is rejected with the same message as null (TUI parity).
        var inputFile = CreateTestFile("input.json", """{"orders":[{"id":1}]}""");

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
            DataFormat.JsonObject, inputFile, drillDownKeyPath: [], CancellationToken.None);

        // Assert
        namesResult.IsFailure.Should().BeTrue();
        namesResult.Error.Should().Be("This recipe's DrillDown path is empty, which is not valid for a JSON Object file.");
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonObjectAndUnresolvableKeyPath_ReturnsFailure()
    {
        // Arrange
        var inputFile = CreateTestFile("input.json", """{"orders":[{"id":1}]}""");
        var keyPath = new KeyPathSegment[] { new("missing", KeyPathSegmentKind.Key) };

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
            DataFormat.JsonObject, inputFile, drillDownKeyPath: keyPath, CancellationToken.None);

        // Assert
        namesResult.IsFailure.Should().BeTrue();
        namesResult.Error.Should().Contain("\"missing\" was not found");
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonObjectResolvingToNonArrayNode_ReturnsFailure()
    {
        // Arrange — the KeyPath resolves, but to an object rather than an array of rows.
        var inputFile = CreateTestFile("input.json", """{"meta":{"version":1}}""");
        var keyPath = new KeyPathSegment[] { new("meta", KeyPathSegmentKind.Key) };

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
            DataFormat.JsonObject, inputFile, drillDownKeyPath: keyPath, CancellationToken.None);

        // Assert
        namesResult.IsFailure.Should().BeTrue();
        namesResult.Error.Should().Be("Node is not a JSON Array.");
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonObjectResolvingToEmptyArray_ReturnsFailure()
    {
        // Arrange
        var inputFile = CreateTestFile("input.json", """{"orders":[]}""");
        var keyPath = new KeyPathSegment[] { new("orders", KeyPathSegmentKind.Key) };

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
            DataFormat.JsonObject, inputFile, drillDownKeyPath: keyPath, CancellationToken.None);

        // Assert
        namesResult.IsFailure.Should().BeTrue();
        namesResult.Error.Should().Be("Array is empty.");
    }

    [Fact]
    public async Task ResolveColumnNames_WithJsonObjectResolvingToKeylessChildren_ReturnsFailure()
    {
        // Arrange
        var inputFile = CreateTestFile("input.json", """{"orders":[{},{}]}""");
        var keyPath = new KeyPathSegment[] { new("orders", KeyPathSegmentKind.Key) };

        // Act
        var namesResult = await ColumnNameResolver.ResolveColumnNamesAsync(
            DataFormat.JsonObject, inputFile, drillDownKeyPath: keyPath, CancellationToken.None);

        // Assert
        namesResult.IsFailure.Should().BeTrue();
        namesResult.Error.Should().Be("All child objects have no keys");
    }

    private string CreateTestFile(string fileName, string content)
    {
        var filePath = Path.Combine(_testDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }
}

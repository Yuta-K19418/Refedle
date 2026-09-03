using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.App.Cli.Factories;
using Refedle.Engine;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.App.Cli;

public sealed partial class FullAggregationRecordReaderTests : IDisposable
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
    public async Task MoveNextAsync_IteratesEveryRowAcrossBatchBoundaryThenStops()
    {
        // Arrange — 1501 records exceed BatchSize (1000), forcing a second ReadBatch.
        var records = Enumerable.Range(0, 1501).Select(i => $$"""{"id":{{i}}}""").ToArray();
        using IRecordReader reader = await BuildReaderAsync(records, KeyPath(), ["id"]);

        // Act
        var (count, lastId) = await DrainColumn0Async(reader);

        // Assert
        count.Should().Be(1501);
        lastId.Should().Be("1500");
    }

    [Fact]
    public async Task GetCellData_DelegatesToTypedCellReaderByOutputColumn()
    {
        // Arrange
        using var reader = await BuildReaderAsync(
            ["""{"orders":[{"id":42,"item":"widget"}]}"""], KeyPath("orders"), ["id", "item"]);

        // Act
        await reader.MoveNextAsync(default);
        var id = reader.GetCellData(0).Value.ToString();
        var item = reader.GetCellData(1).Value.ToString();

        // Assert
        id.Should().Be("42");
        item.Should().Be("widget");
    }

    [Fact]
    public async Task MoveNextAsync_WithZeroRecordArray_ReturnsFalseWithoutReadingABatch()
    {
        // Arrange — "[]" indexes to zero rows; the reader must never call ReadBatch.
        using var reader = await BuildReaderAsync([], KeyPath(), ["id"]);

        // Act
        var hasNext = await reader.MoveNextAsync(default);

        // Assert
        hasNext.Should().BeFalse();
    }

    [Fact]
    public async Task MoveNextAsync_PathAbsentInEveryRecord_YieldsNothing()
    {
        // Arrange
        using var reader = await BuildReaderAsync(["""{"other":1}""", """{"other":2}"""], KeyPath("orders"), ["id"]);

        // Act
        var hasNext = await reader.MoveNextAsync(default);

        // Assert
        hasNext.Should().BeFalse();
    }

    [Fact]
    public async Task MoveNextAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var reader = await BuildReaderAsync(["""{"id":1}"""], KeyPath(), ["id"]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await reader.MoveNextAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EvaluateFilters_MatchingFilter_ReturnsTrue()
    {
        // Arrange
        BatchFilterSpec[] filters = [new(0, ComparisonType.Text, FilterOperator.Equals, "hello")];
        using var reader = await BuildReaderAsync(["""{"v":"hello"}"""], KeyPath(), ["v"], filters);
        await reader.MoveNextAsync(default);

        // Act
        var matched = reader.EvaluateFilters();

        // Assert
        matched.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateFilters_NonMatchingFilter_ReturnsFalse()
    {
        // Arrange
        BatchFilterSpec[] filters = [new(0, ComparisonType.Text, FilterOperator.Equals, "world")];
        using var reader = await BuildReaderAsync(["""{"v":"hello"}"""], KeyPath(), ["v"], filters);
        await reader.MoveNextAsync(default);

        // Act
        var matched = reader.EvaluateFilters();

        // Assert
        matched.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateFilters_NullJsonValue_ReturnsFalse()
    {
        // Arrange — JSON null extracts to the "<null>" sentinel, which the reader rejects.
        BatchFilterSpec[] filters = [new(0, ComparisonType.Text, FilterOperator.Equals, "anything")];
        using var reader = await BuildReaderAsync(["""{"v":null}"""], KeyPath(), ["v"], filters);
        await reader.MoveNextAsync(default);

        // Act
        var matched = reader.EvaluateFilters();

        // Assert
        matched.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateFilters_FilterColumnMissingFromRow_ReturnsFalse()
    {
        // Arrange — "v" is absent from the leaf object, so extraction yields a rejected sentinel.
        BatchFilterSpec[] filters = [new(0, ComparisonType.Text, FilterOperator.Equals, "anything")];
        using var reader = await BuildReaderAsync(["""{"orders":[{"other":1}]}"""], KeyPath("orders"), ["v"], filters);
        await reader.MoveNextAsync(default);

        // Act
        var matched = reader.EvaluateFilters();

        // Assert
        matched.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateFilters_FilterColumnAbsentFromInputSchema_IgnoresFilter()
    {
        // Arrange — a filter whose source index is not in the input schema is skipped, not failed.
        BatchFilterSpec[] filters = [new(9, ComparisonType.Text, FilterOperator.Equals, "anything")];
        using var reader = await BuildReaderAsync(["""{"v":"hello"}"""], KeyPath(), ["v"], filters);
        await reader.MoveNextAsync(default);

        // Act
        var matched = reader.EvaluateFilters();

        // Assert
        matched.Should().BeTrue();
    }

    [Fact]
    public async Task MoveNextAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var reader = await BuildReaderAsync(["""{"id":1}"""], KeyPath(), ["id"]);
        reader.Dispose();

        // Act
        var act = async () => await reader.MoveNextAsync(default);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task GetCellData_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var reader = await BuildReaderAsync(["""{"id":1}"""], KeyPath(), ["id"]);
        await reader.MoveNextAsync(default);
        reader.Dispose();

        // Act
        Action act = () => { _ = reader.GetCellData(0); };

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task EvaluateFilters_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var reader = await BuildReaderAsync(["""{"id":1}"""], KeyPath(), ["id"]);
        await reader.MoveNextAsync(default);
        reader.Dispose();

        // Act
        var act = () => reader.EvaluateFilters();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    // Drains the reader without a loop in the test body (testing rules forbid control flow there).
    // The boxed IRecordReader keeps the mutating struct's cursor state across calls.
    private static async Task<(int count, string lastColumn0)> DrainColumn0Async(IRecordReader reader)
    {
        var count = 0;
        var last = "";
        while (await reader.MoveNextAsync(default))
        {
            count++;
            last = reader.GetCellData(0).Value.ToString();
        }

        return (count, last);
    }

    private static IReadOnlyList<KeyPathSegment> KeyPath(params string[] segments)
        => [.. segments.Select(static s => new KeyPathSegment(s, KeyPathSegmentKind.Key))];

    private Task<FullAggregationRecordReader<JsonArrayBatchSourceReader>> BuildReaderAsync(
        IReadOnlyList<string> records,
        IReadOnlyList<KeyPathSegment> keyPath,
        IReadOnlyList<string> columns) =>
        BuildReaderAsync(records, keyPath, columns, []);

    private async Task<FullAggregationRecordReader<JsonArrayBatchSourceReader>> BuildReaderAsync(
        IReadOnlyList<string> records,
        IReadOnlyList<KeyPathSegment> keyPath,
        IReadOnlyList<string> columns,
        IReadOnlyList<BatchFilterSpec> filters)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, $"[{string.Join(",", records)}]");
        _tempFiles.Add(path);

        var outputSchema = new BatchOutputSchema([.. columns.Select(c => new BatchOutputColumn(c, c))], filters);
        return await new JsonArrayRecordReaderFactory().CreateAsync(
            path, keyPath, columns, outputSchema, new TestAppLogger(), CancellationToken.None);
    }
}

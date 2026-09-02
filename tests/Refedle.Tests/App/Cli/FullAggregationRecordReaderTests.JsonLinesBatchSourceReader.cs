using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.Engine;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.App.Cli;

public sealed partial class FullAggregationRecordReaderTests
{
    [Fact]
    public async Task JsonLines_MoveNextAsync_IteratesEveryRowAcrossBatchBoundaryThenStops()
    {
        // Arrange — 1501 lines exceed BatchSize (1000), forcing a second ReadBatch.
        var lines = Enumerable.Range(0, 1501).Select(i => $$"""{"id":{{i}}}""").ToArray();
        using IRecordReader reader = BuildJsonLinesReader(lines, KeyPath(), ["id"]);

        // Act
        var (count, lastId) = await DrainColumn0Async(reader);

        // Assert
        count.Should().Be(1501);
        lastId.Should().Be("1500");
    }

    [Fact]
    public async Task JsonLines_GetCellData_DelegatesToTypedCellReaderByOutputColumn()
    {
        // Arrange
        using var reader = BuildJsonLinesReader(
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
    public async Task JsonLines_MoveNextAsync_WithZeroRecordFile_ReturnsFalseWithoutReadingABatch()
    {
        // Arrange — an empty file indexes to zero rows and yields no RowReader.
        using var reader = BuildJsonLinesReader([], KeyPath(), ["id"]);

        // Act
        var hasNext = await reader.MoveNextAsync(default);

        // Assert
        hasNext.Should().BeFalse();
    }

    [Fact]
    public async Task JsonLines_MoveNextAsync_PathAbsentInEveryRecord_YieldsNothing()
    {
        // Arrange
        using var reader = BuildJsonLinesReader(["""{"other":1}""", """{"other":2}"""], KeyPath("orders"), ["id"]);

        // Act
        var hasNext = await reader.MoveNextAsync(default);

        // Assert
        hasNext.Should().BeFalse();
    }

    [Fact]
    public async Task JsonLines_EvaluateFilters_NonMatchingFilter_ReturnsFalse()
    {
        // Arrange
        BatchFilterSpec[] filters = [new(0, ComparisonType.Text, FilterOperator.Equals, "world")];
        using var reader = BuildJsonLinesReader(["""{"v":"hello"}"""], KeyPath(), ["v"], filters);
        await reader.MoveNextAsync(default);

        // Act
        var matched = reader.EvaluateFilters();

        // Assert
        matched.Should().BeFalse();
    }

    [Fact]
    public async Task JsonLines_MoveNextAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var reader = BuildJsonLinesReader(["""{"id":1}"""], KeyPath(), ["id"]);
        reader.Dispose();

        // Act
        var act = async () => await reader.MoveNextAsync(default);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    private FullAggregationRecordReader<JsonLinesBatchSourceReader> BuildJsonLinesReader(
        IReadOnlyList<string> lines,
        IReadOnlyList<KeyPathSegment> keyPath,
        IReadOnlyList<string> columns) =>
        BuildJsonLinesReader(lines, keyPath, columns, []);

    private FullAggregationRecordReader<JsonLinesBatchSourceReader> BuildJsonLinesReader(
        IReadOnlyList<string> lines,
        IReadOnlyList<KeyPathSegment> keyPath,
        IReadOnlyList<string> columns,
        IReadOnlyList<BatchFilterSpec> filters)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
        File.WriteAllText(path, lines.Count == 0 ? "" : string.Join("\n", lines) + "\n");
        _tempFiles.Add(path);

        var rowIndexer = new RowIndexer(path);
        rowIndexer.BuildIndex(CancellationToken.None);
        var rowReader = rowIndexer.TotalRows == 0 ? null : new RowReader(path);

        var outputSchema = new BatchOutputSchema([.. columns.Select(c => new BatchOutputColumn(c, c))], filters);
        return new FullAggregationRecordReader<JsonLinesBatchSourceReader>(
            rowIndexer, new JsonLinesBatchSourceReader(rowReader), keyPath, columns, outputSchema);
    }
}

using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.App.Cli.Factories;
using Refedle.Engine;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.App.Cli;

// ADR-6: JsonLinesRecordReader is a union dispatch struct. A member that forgets its
// `_isDrillDown` branch would silently run the wrong reader — every IRecordReader member is
// exercised here in both modes, with fixtures built so both modes yield the same rows.
public sealed class JsonLinesRecordReaderTests : IDisposable
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispatch_MoveNextAndGetCellData_ForwardToTheSelectedReader(bool drillDown)
    {
        // Arrange
        using var reader = await BuildReaderAsync(drillDown, ["id", "item"], []);

        // Act
        var items = await DrainColumnAsync(reader, 1);

        // Assert
        items.Should().Equal(["a", "b"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispatch_EvaluateFilters_ForwardsToTheSelectedReader(bool drillDown)
    {
        // Arrange — the filter keeps only the row whose item is "a".
        BatchFilterSpec[] filters = [new(1, ComparisonType.Text, FilterOperator.Equals, "a")];
        using var reader = await BuildReaderAsync(drillDown, ["id", "item"], filters);

        // Act
        await reader.MoveNextAsync(default);
        var firstMatches = reader.EvaluateFilters();
        await reader.MoveNextAsync(default);
        var secondMatches = reader.EvaluateFilters();

        // Assert
        firstMatches.Should().BeTrue();
        secondMatches.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispatch_AfterDispose_MoveNextThrows(bool drillDown)
    {
        // Arrange
        var reader = await BuildReaderAsync(drillDown, ["id"], []);
        reader.Dispose();

        // Act
        var act = async () => await reader.MoveNextAsync(default);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // Drains one column without a loop in the test body. The struct is advanced in this local
    // copy; callers only read the returned values.
    private static async Task<IReadOnlyList<string>> DrainColumnAsync(JsonLinesRecordReader reader, int columnIndex)
    {
        List<string> values = [];
        while (await reader.MoveNextAsync(default))
        {
            values.Add(reader.GetCellData(columnIndex).Value.ToString());
        }

        return values;
    }

    private async Task<JsonLinesRecordReader> BuildReaderAsync(
        bool drillDown,
        IReadOnlyList<string> columns,
        IReadOnlyList<BatchFilterSpec> filters)
    {
        // Both fixtures yield rows [(id:1,item:a),(id:2,item:b)]: bare reads each line, the
        // DrillDown path traverses "orders" in the single line.
        var content = drillDown
            ? """{"orders":[{"id":1,"item":"a"},{"id":2,"item":"b"}]}""" + "\n"
            : "{\"id\":1,\"item\":\"a\"}\n{\"id\":2,\"item\":\"b\"}\n";
        IReadOnlyList<KeyPathSegment>? keyPath = drillDown
            ? [new KeyPathSegment("orders", KeyPathSegmentKind.Key)]
            : null;

        var path = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);

        var outputSchema = new BatchOutputSchema([.. columns.Select(c => new BatchOutputColumn(c, c))], filters);
        return await new JsonLinesRecordReaderFactory().CreateAsync(
            path, keyPath, columns, outputSchema, new TestAppLogger(), CancellationToken.None);
    }
}

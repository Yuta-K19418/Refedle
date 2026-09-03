using AwesomeAssertions;
using Refedle.App.Cli.Factories;
using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.Tests.App.Cli.Factories;

public sealed class JsonLinesRecordReaderFactoryTests : IDisposable
{
    private readonly string _testDir;

    public JsonLinesRecordReaderFactoryTests()
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
    public async Task CreateAsync_WithNullKeyPath_ReturnsBareReaderOverEveryLine()
    {
        // Arrange — a null KeyPath selects the bare (non-DrillDown) arm.
        var inputFile = CreateTestFile("input.jsonl", "{\"id\":1}\n{\"id\":2}\n");
        var factory = new JsonLinesRecordReaderFactory();

        // Act
        using var reader = await factory.CreateAsync(
            inputFile, drillDownKeyPath: null, ["id"], BuildOutputSchema("id"), new TestAppLogger(), CancellationToken.None);

        // Assert
        (await reader.MoveNextAsync(default)).Should().BeTrue();
        reader.GetCellData(0).Value.ToString().Should().Be("1");
        (await reader.MoveNextAsync(default)).Should().BeTrue();
        reader.GetCellData(0).Value.ToString().Should().Be("2");
        (await reader.MoveNextAsync(default)).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithKeyPath_ReturnsFullAggregationReaderOverDrilledDownRows()
    {
        // Arrange
        var inputFile = CreateTestFile(
            "input.jsonl",
            "{\"orders\":[{\"id\":1,\"item\":\"a\"}]}\n{\"orders\":[{\"id\":2,\"item\":\"b\"}]}\n");
        var keyPath = new KeyPathSegment[] { new("orders", KeyPathSegmentKind.Key) };
        var factory = new JsonLinesRecordReaderFactory();

        // Act
        using var reader = await factory.CreateAsync(
            inputFile, keyPath, ["id", "item"], BuildOutputSchema("id", "item"), new TestAppLogger(), CancellationToken.None);

        // Assert
        (await reader.MoveNextAsync(default)).Should().BeTrue();
        reader.GetCellData(0).Value.ToString().Should().Be("1");
        reader.GetCellData(1).Value.ToString().Should().Be("a");
        (await reader.MoveNextAsync(default)).Should().BeTrue();
        reader.GetCellData(0).Value.ToString().Should().Be("2");
        (await reader.MoveNextAsync(default)).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange — the factory forwards its token to RowIndexer.BuildIndex.
        var inputFile = CreateTestFile("input.jsonl", "{\"id\":1}\n");
        var factory = new JsonLinesRecordReaderFactory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await factory.CreateAsync(
            inputFile, drillDownKeyPath: null, ["id"], BuildOutputSchema("id"), new TestAppLogger(), cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static BatchOutputSchema BuildOutputSchema(params string[] columnNames) =>
        new([.. columnNames.Select(name => new BatchOutputColumn(name, name))], []);

    private string CreateTestFile(string fileName, string content)
    {
        var filePath = Path.Combine(_testDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }
}

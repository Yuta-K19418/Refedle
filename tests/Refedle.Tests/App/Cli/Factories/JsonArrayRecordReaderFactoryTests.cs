using System.Text.Json;
using AwesomeAssertions;
using Refedle.App.Cli.Factories;
using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.Tests.App.Cli.Factories;

public sealed class JsonArrayRecordReaderFactoryTests : IDisposable
{
    private readonly string _testDir;

    public JsonArrayRecordReaderFactoryTests()
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
    public async Task CreateAsync_WithNullKeyPath_ThrowsInvalidOperationException()
    {
        // Arrange — null cannot reach the factory: DrillDownRecipeValidator rejects it upstream.
        var inputFile = CreateTestFile("input.json", """[{"orders":[{"id":1}]}]""");
        var factory = new JsonArrayRecordReaderFactory();

        // Act
        var act = async () => await factory.CreateAsync(
            inputFile, drillDownKeyPath: null, ["id"], BuildOutputSchema("id"), new TestAppLogger(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_WithEmptyKeyPath_ReturnsReaderOverWholeRecords()
    {
        // Arrange — an empty path is valid: each top-level record is itself the leaf row.
        var inputFile = CreateTestFile("input.json", """[{"id":1},{"id":2}]""");
        var factory = new JsonArrayRecordReaderFactory();

        // Act
        using var reader = await factory.CreateAsync(
            inputFile, drillDownKeyPath: [], ["id"], BuildOutputSchema("id"), new TestAppLogger(), CancellationToken.None);

        // Assert
        (await reader.MoveNextAsync(default)).Should().BeTrue();
        reader.GetCellData(0).Value.ToString().Should().Be("1");
        (await reader.MoveNextAsync(default)).Should().BeTrue();
        reader.GetCellData(0).Value.ToString().Should().Be("2");
        (await reader.MoveNextAsync(default)).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithValidKeyPath_ReturnsReaderOverDrilledDownRows()
    {
        // Arrange
        var inputFile = CreateTestFile(
            "input.json", """[{"orders":[{"id":1,"item":"a"}]},{"orders":[{"id":2,"item":"b"}]}]""");
        var keyPath = new KeyPathSegment[] { new("orders", KeyPathSegmentKind.Key) };
        var factory = new JsonArrayRecordReaderFactory();

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
    public async Task CreateAsync_WithPathAbsentInEveryRecord_ReturnsReaderThatYieldsNoRows()
    {
        // Arrange — the factory does no schema resolution, so a non-matching path is not an error;
        // the traverser simply skips every record and the reader yields nothing.
        var inputFile = CreateTestFile("input.json", """[{"other":1},{"other":2}]""");
        var keyPath = new KeyPathSegment[] { new("orders", KeyPathSegmentKind.Key) };
        var factory = new JsonArrayRecordReaderFactory();

        // Act
        using var reader = await factory.CreateAsync(
            inputFile, keyPath, ["id"], BuildOutputSchema("id"), new TestAppLogger(), CancellationToken.None);

        // Assert
        (await reader.MoveNextAsync(default)).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange — the factory forwards its token to RowIndexer.BuildIndex.
        var inputFile = CreateTestFile("input.json", """[{"id":1}]""");
        var factory = new JsonArrayRecordReaderFactory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await factory.CreateAsync(
            inputFile, [], ["id"], BuildOutputSchema("id"), new TestAppLogger(), cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CreateAsync_WithTruncatedJsonArray_ThrowsJsonException()
    {
        // Arrange — the index build rejects an unterminated root array.
        var inputFile = CreateTestFile("input.json", """[{"id":1}""");
        var factory = new JsonArrayRecordReaderFactory();

        // Act
        var act = async () => await factory.CreateAsync(
            inputFile, [], ["id"], BuildOutputSchema("id"), new TestAppLogger(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<JsonException>();
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

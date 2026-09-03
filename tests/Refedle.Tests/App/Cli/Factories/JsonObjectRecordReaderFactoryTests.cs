using AwesomeAssertions;
using Refedle.App.Cli.Factories;
using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.Tests.App.Cli.Factories;

public sealed class JsonObjectRecordReaderFactoryTests : IDisposable
{
    private readonly string _testDir;

    public JsonObjectRecordReaderFactoryTests()
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
        // Arrange
        var inputFile = CreateTestFile("input.json", """{"orders":[{"id":1}]}""");
        var factory = new JsonObjectRecordReaderFactory();

        // Act
        var act = async () => await factory.CreateAsync(inputFile, drillDownKeyPath: null, ["id"], BuildOutputSchema(), new TestAppLogger(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_WithEmptyKeyPath_ThrowsInvalidOperationException()
    {
        // Arrange
        var inputFile = CreateTestFile("input.json", """{"orders":[{"id":1}]}""");
        var factory = new JsonObjectRecordReaderFactory();

        // Act
        var act = async () => await factory.CreateAsync(inputFile, drillDownKeyPath: [], ["id"], BuildOutputSchema(), new TestAppLogger(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_WithUnresolvableKeyPath_ThrowsInvalidOperationException()
    {
        // Arrange
        var inputFile = CreateTestFile("input.json", """{"orders":[{"id":1}]}""");
        var keyPath = new KeyPathSegment[] { new("missing", KeyPathSegmentKind.Key) };
        var factory = new JsonObjectRecordReaderFactory();

        // Act
        var act = async () => await factory.CreateAsync(inputFile, keyPath, ["id"], BuildOutputSchema(), new TestAppLogger(), CancellationToken.None);

        // Assert — the underlying resolver's error message is surfaced as-is.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*\"missing\" was not found*");
    }

    [Fact]
    public async Task CreateAsync_WhenNodeIsNotArray_ThrowsInvalidOperationException()
    {
        // Arrange — the KeyPath resolves, but to an object rather than an array of rows.
        var inputFile = CreateTestFile("input.json", """{"meta":{"version":1}}""");
        var keyPath = new KeyPathSegment[] { new("meta", KeyPathSegmentKind.Key) };
        var factory = new JsonObjectRecordReaderFactory();

        // Act
        var act = async () => await factory.CreateAsync(inputFile, keyPath, ["version"], BuildOutputSchema(), new TestAppLogger(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Node is not a JSON Array.");
    }

    [Fact]
    public async Task CreateAsync_WithValidKeyPath_ReturnsReaderOverResolvedRows()
    {
        // Arrange
        var inputFile = CreateTestFile("input.json", """{"orders":[{"id":1,"item":"a"},{"id":2,"item":"b"}]}""");
        var keyPath = new KeyPathSegment[] { new("orders", KeyPathSegmentKind.Key) };
        var factory = new JsonObjectRecordReaderFactory();

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

    private static BatchOutputSchema BuildOutputSchema(params string[] columnNames) =>
        new([.. columnNames.Select(name => new BatchOutputColumn(name, name))], []);

    private string CreateTestFile(string fileName, string content)
    {
        var filePath = Path.Combine(_testDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }
}

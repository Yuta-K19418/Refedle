using AwesomeAssertions;
using Refedle.App.Cli.Factories;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli.Factories;

public sealed class JsonArrayRecordWriterFactoryTests : IDisposable
{
    private readonly string _testDir;

    public JsonArrayRecordWriterFactoryTests()
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
    public async Task CreateAsync_ReturnsWriterThatPersistsAJsonArrayToTheOutputPath()
    {
        // Arrange
        var outputFile = Path.Combine(_testDir, "out.json");
        var factory = new JsonArrayRecordWriterFactory();
        var outputSchema = new BatchOutputSchema([new BatchOutputColumn("value", "value")], []);

        // Act
        await using (var writer = await factory.CreateAsync(outputFile, outputSchema, new TestAppLogger(), CancellationToken.None))
        {
            await writer.WriteHeaderAsync(default);
            await writer.WriteFooterAsync(default);
            await writer.FlushAsync(default);
        }

        // Assert
        File.ReadAllText(outputFile).Should().Be("[]");
    }
}

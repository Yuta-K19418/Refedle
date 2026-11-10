using AwesomeAssertions;
using Refedle.App.Cli.IO;
using Refedle.App.Cli.IO.Factories;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli.IO.Factories;

public sealed class CsvRecordWriterFactoryTests() : IDisposable
{
    private readonly string _testDir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())).FullName;

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAsync_ReturnsWriterThatWritesCsvWithoutUtf8Bom()
    {
        // Arrange
        var outputFile = Path.Combine(_testDir, "out.csv");
        var factory = new CsvRecordWriterFactory();
        var outputSchema = new BatchOutputSchema([new BatchOutputColumn("value", "value")], []);

        // Act
        await using (var writer = await factory.CreateAsync(outputFile, outputSchema, new TestAppLogger(), CancellationToken.None))
        {
            await writer.WriteHeaderAsync(default);
            await writer.WriteStartRecordAsync(default);
            writer.WriteCellData(0, new CellData("a", CellPresence.Value, CellEncoding.PlainText));
            await writer.WriteEndRecordAsync(default);
            await writer.FlushAsync(default);
        }

        // Assert — check the BOM bytes literally so the test does not depend on the input-side utility.
        var bytes = File.ReadAllBytes(outputFile);
        byte[] utf8Bom = [0xEF, 0xBB, 0xBF];
        bytes.AsSpan(0, 3).SequenceEqual(utf8Bom).Should().BeFalse();
        File.ReadAllText(outputFile).Should().Be("value\na\n");
    }
}

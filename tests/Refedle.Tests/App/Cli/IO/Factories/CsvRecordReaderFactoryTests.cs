using AwesomeAssertions;
using Refedle.App.Cli.IO.Factories;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli.IO.Factories;

public sealed class CsvRecordReaderFactoryTests : IDisposable
{
    private readonly string _testDir;

    public CsvRecordReaderFactoryTests()
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
    public async Task CreateAsync_WithQuotedCell_ReturnsValueWithoutQuotes()
    {
        // Arrange
        var inputFile = Path.Combine(_testDir, "input.csv");
        // Header: id,name
        var headerLine = "id,name";
        // Data: 1,"say ""hi"""
        var dataLine = "1,\"say \"\"hi\"\"\"";
        string[] lines = [headerLine, dataLine];
        var csvContent = string.Join("\n", lines) + "\n";
        await File.WriteAllTextAsync(inputFile, csvContent);
        var factory = new CsvRecordReaderFactory();
        var outputSchema = new BatchOutputSchema([new BatchOutputColumn("name", "name")], []);

        // Act
        using var reader = await factory.CreateAsync(
            inputFile, drillDownKeyPath: null, ["id", "name"], outputSchema, new TestAppLogger(), CancellationToken.None);

        // Assert
        // Expected: say "hi"
        (await reader.MoveNextAsync(default)).Should().BeTrue();
        reader.GetCellData(0).Value.ToString().Should().Be("say \"hi\"");
    }
}

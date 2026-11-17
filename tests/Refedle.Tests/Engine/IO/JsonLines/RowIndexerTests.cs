using AwesomeAssertions;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed partial class RowIndexerTests : IDisposable
{
    private readonly string _testFilePath;

    public RowIndexerTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonlinesRowIndexer_{Guid.NewGuid()}.jsonl"
        );
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void Constructor_WithNullFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        Func<RowIndexer> act = () => new RowIndexer(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhiteSpaceFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        Func<RowIndexer> act = () => new RowIndexer("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithValidFilePath_SetsFilePathProperty()
    {
        // Arrange
        string expectedPath = "/path/to/file.jsonl";

        // Act
        RowIndexer indexer = new(expectedPath);

        // Assert
        indexer.FilePath.Should().Be(expectedPath);
    }

    [Fact]
    public void TotalRows_BeforeBuildIndex_ReturnsZero()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{}");
        RowIndexer indexer = new(_testFilePath);

        // Act
        long totalRows = indexer.TotalRows;

        // Assert
        totalRows.Should().Be(0);
    }
}

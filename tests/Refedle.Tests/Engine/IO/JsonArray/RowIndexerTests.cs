using AwesomeAssertions;
using Refedle.Engine.IO.JsonArray;

namespace Refedle.Tests.Engine.IO.JsonArray;

public sealed partial class RowIndexerTests : IDisposable
{
    private readonly string _testFilePath;

    public RowIndexerTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonarrayRowIndexer_{Guid.NewGuid()}.json"
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
        // Arrange — no setup required

        // Act
        Func<RowIndexer> act = () => new RowIndexer(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhiteSpaceFilePath_ThrowsArgumentException()
    {
        // Arrange — no setup required

        // Act
        Func<RowIndexer> act = () => new RowIndexer("   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithValidFilePath_SetsFilePathProperty()
    {
        // Arrange
        string expectedPath = "/path/to/file.json";

        // Act
        RowIndexer indexer = new(expectedPath);

        // Assert
        indexer.FilePath.Should().Be(expectedPath);
    }

    [Fact]
    public void TotalRows_BeforeBuildIndex_ReturnsZero()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[]");
        RowIndexer indexer = new(_testFilePath);

        // Act
        long totalRows = indexer.TotalRows;

        // Assert
        totalRows.Should().Be(0);
    }
}

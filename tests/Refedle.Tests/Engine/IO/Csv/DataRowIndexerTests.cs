using AwesomeAssertions;
using Refedle.Engine.IO.Csv;

namespace Refedle.Tests.Engine.IO.Csv;

public sealed partial class DataRowIndexerTests : IDisposable
{
    private readonly string _testFilePath;

    public DataRowIndexerTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"csvRowIndexer_{Guid.NewGuid()}.csv");
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
        Func<DataRowIndexer> act = () => new DataRowIndexer(null!);
        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("Value cannot be null.*")
            .WithParameterName("filePath");
    }

    [Fact]
    public void Constructor_WithEmptyFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        Func<DataRowIndexer> act = () => new DataRowIndexer(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhitespaceFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        Func<DataRowIndexer> act = () => new DataRowIndexer("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TotalRows_BeforeBuildIndex_ReturnsZero()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2");
        DataRowIndexer indexer = new(_testFilePath);

        // Act & Assert
        indexer.TotalRows.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNonExistentFile_DoesNotThrow()
    {
        // Arrange
        string nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.csv");

        // Act & Assert (constructor should not throw for non-existent file)
        DataRowIndexer indexer = new(nonExistentPath);
        indexer.FilePath.Should().Be(nonExistentPath);
        indexer.TotalRows.Should().Be(0);
    }
}

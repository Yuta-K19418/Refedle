using AwesomeAssertions;
using Refedle.Engine.IO.Csv;

namespace Refedle.Tests.Engine.IO.Csv;

public sealed class ColumnNameScannerTests : IDisposable
{
    private readonly string _testFilePath;

    public ColumnNameScannerTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"columnNameScanner_{Guid.NewGuid()}.csv");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void ScanColumnNames_WithNormalHeader_ReturnsNamesInOrder()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "name,age,city\nAlice,30,NYC");

        // Act
        var names = ColumnNameScanner.ScanColumnNames(_testFilePath);

        // Assert
        names.Should().Equal(["name", "age", "city"]);
    }

    [Fact]
    public void ScanColumnNames_WithHeaderOnlyFile_ReturnsNamesWithoutReadingDataRows()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "name,age\n");

        // Act
        var names = ColumnNameScanner.ScanColumnNames(_testFilePath);

        // Assert
        names.Should().Equal(["name", "age"]);
    }

    [Theory]
    [InlineData("name,,age")]  // empty header cell
    [InlineData("name, ,age")] // whitespace-only header cell
    public void ScanColumnNames_WithBlankHeaderCell_AutoNamesColumn(string header)
    {
        // Arrange
        File.WriteAllText(_testFilePath, $"{header}\nAlice,30,NYC");

        // Act
        var names = ColumnNameScanner.ScanColumnNames(_testFilePath);

        // Assert
        names.Should().Equal(["name", "Column2", "age"]);
    }

    [Fact]
    public void ScanColumnNames_WithDuplicateHeaderNames_ThrowsArgumentException()
    {
        // Arrange — Sep itself rejects exact duplicate header names at FromFile time, before
        // ScanColumnNames' own uniqueness check runs (same behavior as the old IncrementalSchemaScanner path).
        File.WriteAllText(_testFilePath, "name,age,name\nAlice,30,NYC");

        // Act
        Action act = () => ColumnNameScanner.ScanColumnNames(_testFilePath);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*'name'*");
    }

    [Fact]
    public void ScanColumnNames_WithAutoNamedCollisionWithExplicitName_ThrowsInvalidOperationException()
    {
        // Arrange — the blank cell at index 1 auto-names to "Column2", colliding with the
        // explicit "Column2" header at index 0; auto-naming and explicit names share the
        // same uniqueness check.
        File.WriteAllText(_testFilePath, "Column2,,age\nAlice,30,NYC");

        // Act
        Action act = () => ColumnNameScanner.ScanColumnNames(_testFilePath);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*'Column2'*");
    }
}

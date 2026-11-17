using AwesomeAssertions;
using Refedle.Engine.IO.Csv;

namespace Refedle.Tests.Engine.IO.Csv;

public sealed class DataRowCacheTests : IDisposable
{
    private readonly string _testFilePath;

    public DataRowCacheTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"csvRowCache_{Guid.NewGuid()}.csv");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    private static string[] ToStringArray(CsvDataRow row)
    {
        string[] result = new string[row.Count];
        for (int i = 0; i < row.Count; i++)
        {
            result[i] = row[i].IsEmpty ? string.Empty : new string(row[i].Span);
        }

        return result;
    }

    private static string BuildCsvWith20DataRows()
    {
        string header = "col1,col2";
        IEnumerable<string> dataRows = Enumerable.Range(0, 20).Select(i => $"val{i * 2 + 1},val{i * 2 + 2}");
        return string.Join("\n", [header, .. dataRows]);
    }

    [Fact]
    public void GetRow_WithValidIndex_ReturnsCorrectRow()
    {
        // Arrange
        string csvContent = "col1,col2,col3\nval1,val2,val3\nval4,val5,val6\nval7,val8,val9";
        File.WriteAllText(_testFilePath, csvContent);
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        using DataRowCache cache = new(indexer, columnCount: 3, capacity: 10, prefetchWindow: 20);

        // Act
        CsvDataRow row = cache.GetRow(0);

        // Assert
        ToStringArray(row).Should().Equal(["val1", "val2", "val3"]);
    }

    [Fact]
    public void GetRow_WithMultipleRequests_UsesCacheEfficiently()
    {
        // Arrange
        string csvContent = "col1,col2\nval1,val2\nval3,val4\nval5,val6";
        File.WriteAllText(_testFilePath, csvContent);
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        using DataRowCache cache = new(indexer, columnCount: 2, capacity: 10, prefetchWindow: 20);

        // Act - Request same row multiple times
        CsvDataRow row1 = cache.GetRow(0);
        CsvDataRow row2 = cache.GetRow(0);
        CsvDataRow row3 = cache.GetRow(1);

        // Assert
        ToStringArray(row1).Should().Equal(["val1", "val2"]);
        ToStringArray(row2).Should().Equal(["val1", "val2"]);
        ToStringArray(row3).Should().Equal(["val3", "val4"]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void GetRow_WithInvalidNegativeIndex_ReturnsEmptyArray(long invalidIndex)
    {
        // Arrange
        string csvContent = "col1,col2\nval1,val2";
        File.WriteAllText(_testFilePath, csvContent);
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        using DataRowCache cache = new(indexer, columnCount: 2, capacity: 200, prefetchWindow: 20);

        // Act
        CsvDataRow row = cache.GetRow(invalidIndex);

        // Assert
        row.Should().BeEmpty();
    }

    [Fact]
    public void GetRow_WithIndexBeyondTotalRows_ReturnsEmptyArray()
    {
        // Arrange
        string csvContent = "col1,col2\nval1,val2";
        File.WriteAllText(_testFilePath, csvContent);
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        using DataRowCache cache = new(indexer, columnCount: 2, capacity: 200, prefetchWindow: 20);

        // Act
        CsvDataRow row = cache.GetRow(100);

        // Assert
        row.Should().BeEmpty();
    }

    [Fact]
    public void GetRow_WithSmallCacheSize_HandlesWindowedReads()
    {
        // Arrange
        string csvContent = BuildCsvWith20DataRows();
        File.WriteAllText(_testFilePath, csvContent);
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        using DataRowCache cache = new(indexer, columnCount: 2, capacity: 5, prefetchWindow: 20);

        // Act - Request rows outside the initial cache window
        CsvDataRow row0 = cache.GetRow(0);
        CsvDataRow row10 = cache.GetRow(10);
        CsvDataRow row19 = cache.GetRow(19);

        // Assert
        ToStringArray(row0).Should().Equal(["val1", "val2"]);
        ToStringArray(row10).Should().Equal(["val21", "val22"]);
        ToStringArray(row19).Should().Equal(["val39", "val40"]);
    }

    [Fact]
    public void TotalRows_ReturnsCorrectCount()
    {
        // Arrange
        string csvContent = "col1,col2\nval1,val2\nval3,val4\nval5,val6\nval7,val8";
        File.WriteAllText(_testFilePath, csvContent);
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        using DataRowCache cache = new(indexer, columnCount: 2, capacity: 200, prefetchWindow: 20);

        // Act & Assert - DataRowIndexer counts data rows excluding header
        cache.TotalRows.Should().Be(4);
    }

    [Fact]
    public void GetRow_WithRaggedCsvDataRow_ThrowsInvalidDataException()
    {
        // Arrange
        string csvContent = "col1,col2,col3\nval1,val2\nval3";
        File.WriteAllText(_testFilePath, csvContent);
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        using DataRowCache cache = new(indexer, columnCount: 3, capacity: 200, prefetchWindow: 20);

        // Act & Assert
        // Sep.Reader enforces strict column count matching by default
        Func<CsvDataRow> act = () => cache.GetRow(0);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void GetRow_WithEmptyFile_ReturnsEmptyRow()
    {
        // Arrange - Only header row
        File.WriteAllText(_testFilePath, "col1,col2");
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        using DataRowCache cache = new(indexer, columnCount: 2, capacity: 200, prefetchWindow: 20);

        // Act & Assert - No data rows exist (only header), so TotalRows should be 0
        // row 0 is out of bounds (no data rows)
        cache.TotalRows.Should().Be(0);
        CsvDataRow row = cache.GetRow(0);
        row.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_DisposesReader()
    {
        // Arrange
        string csvContent = "col1,col2\nval1,val2";
        File.WriteAllText(_testFilePath, csvContent);
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        DataRowCache cache = new(indexer, columnCount: 2);
        cache.GetRow(0);

        // Act
        cache.Dispose();

        // Assert — verify the underlying FileStream was released by acquiring exclusive access
        using FileStream stream = new(_testFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
        stream.Should().NotBeNull();
    }
}

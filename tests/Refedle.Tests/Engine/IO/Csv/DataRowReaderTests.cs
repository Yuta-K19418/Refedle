using AwesomeAssertions;
using Refedle.Engine.IO.Csv;

namespace Refedle.Tests.Engine.IO.Csv;

public sealed class DataRowReaderTests : IDisposable
{
    private readonly string _testFilePath;

    public DataRowReaderTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"csvRowReader_{Guid.NewGuid()}.csv");
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
        var result = new string[row.Count];
        for (var i = 0; i < row.Count; i++)
        {
            result[i] = row[i].IsEmpty ? string.Empty : new string(row[i].Span);
        }

        return result;
    }

    [Fact]
    public void ReadRows_WithValidData_ReturnsCorrectRows()
    {
        // Arrange
        var csvContent = "col1,col2,col3\nval1,val2,val3\nval4,val5,val6\nval7,val8,val9";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 3);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act - Skip header by using offset, then start reading data rows
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2).Value;

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val1", "val2", "val3"]);
        ToStringArray(rows[1]).Should().Equal(["val4", "val5", "val6"]);
    }

    [Fact]
    public void ReadRows_WithNegativeByteOffset_ReturnsEmptyList()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act - Use offset after header for consistency, but negative offset should still return empty
        var rows = reader.ReadRows(byteOffset: -1, rowsToSkip: 0, rowsToRead: 1).Value;

        // Assert
        rows.Should().BeEmpty();
    }

    [Fact]
    public void ReadRows_WithZeroRowsToRead_ReturnsEmptyList()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 0).Value;

        // Assert
        rows.Should().BeEmpty();
    }

    [Theory]
    [InlineData("col1,col2,col3\nval1,val2\nval3,val4", 3)]
    [InlineData("col1,col2\nval1,val2,val3\nval4,val5,val6", 2)]
    [InlineData("col1,col2,col3\nval1,val2\nval3,val4,val5", 3)]
    [InlineData("col1,col2\nval1,val2\nval3,val4,val5", 2)]
    [InlineData("col1,col2\nval1,val2\nval3", 2)]
    public void ReadRows_WhenWindowHasRowWithDifferentColumnCount_ReturnsFailure(
        string csvContent,
        int columnCount)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(csvContent);
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act
        var result = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain($"({columnCount})");
    }

    [Fact]
    public void ReadRows_WhenRowsOutsideWindowAreRagged_ReturnsOnlyWindowRows()
    {
        // Arrange - the ragged row is past the requested window
        var csvContent = "col1,col2\nval1,val2\nval3,val4\nval5";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act
        var result = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(2);
    }

    [Theory]
    [InlineData("col1,col2\nshort\nval1,val2\nval3,val4", 1, 2)]
    [InlineData("col1,col2\nx,y,z\nval1,val2\nval3,val4", 1, 2)]
    [InlineData("col1,col2\nval1,val2\nshort\nval3,val4\nval5,val6", 2, 1)]
    [InlineData("col1,col2\nval1,val2\nx,y,z\nval3,val4\nval5,val6", 2, 1)]
    [InlineData("col1,col2\nshort", 1, 1)]
    [InlineData("col1,col2\nval1,val2\nx,y,z", 2, 1)]
    public void ReadRows_WhenSkippedRowHasDifferentColumnCount_ReturnsFailure(
        string csvContent,
        int rowsToSkip,
        int rowsToRead)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(csvContent);
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act
        var result = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip, rowsToRead);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReadRows_WithNonZeroByteOffset_ReadsFromCorrectPosition()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2\nval3,val4\nval5,val6";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act - When reading from offset after header, the first line at that offset is treated as data (HasHeader = false)
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2).Value;

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val1", "val2"]);
        ToStringArray(rows[1]).Should().Equal(["val3", "val4"]);
    }

    [Fact]
    public void ReadRows_WithRowsToSkip_SkipsCorrectNumberOfRows()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2\nval3,val4\nval5,val6\nval7,val8";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act - Skip header by using offset, then skip additional rows
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 1, rowsToRead: 2).Value;

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val3", "val4"]);
        ToStringArray(rows[1]).Should().Equal(["val5", "val6"]);
    }

    [Fact]
    public void Constructor_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.csv");

        // Act
        var act = () => new DataRowReader(nonExistentPath, columnCount: 2);

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ReadRows_WithMoreRowsRequestedThanAvailable_ReturnsAvailableRows()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2\nval3,val4";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act - Request 100 rows but only 2 data rows available
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 100).Value;

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val1", "val2"]);
        ToStringArray(rows[1]).Should().Equal(["val3", "val4"]);
    }

    [Fact]
    public void Dispose_ReleasesFileStream()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2";
        File.WriteAllText(_testFilePath, csvContent);
        var reader = new DataRowReader(_testFilePath, columnCount: 2);
        reader.ReadRows(0, 0, 1);

        // Act
        reader.Dispose();

        // Assert
        // Try opening file with FileShare.None - should succeed if stream was released
        using var stream = new FileStream(_testFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
        stream.Should().NotBeNull();
    }

    [Fact]
    public void ReadRows_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        reader.Dispose();

        // Act
        var act = () => reader.ReadRows(0, 0, 1);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ReadRows_WithQuotedCell_ReturnsValueWithoutQuotes()
    {
        // Arrange
        // Header: col1,col2
        var headerLine = "col1,col2";
        // Data: "say ""hi""",val2
        var dataLine = "\"say \"\"hi\"\"\",val2";
        string[] lines = [headerLine, dataLine];
        var csvContent = string.Join("\n", lines);
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 1).Value;

        // Assert
        // Expected: say "hi", val2
        ToStringArray(rows[0]).Should().Equal(["say \"hi\"", "val2"]);
    }

    [Fact]
    public void EnumerateRows_WithValidData_YieldsAllDataRows()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2\nval3,val4";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var rows = reader.EnumerateRows(rowsToSkip: 0).ToList();

        // Assert - The header row is skipped
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val1", "val2"]);
        ToStringArray(rows[1]).Should().Equal(["val3", "val4"]);
    }

    [Theory]
    [InlineData("col1,col2,col3\nval1,val2,val3\nval4,val5")]
    [InlineData("col1,col2\nval1,val2\nval3,val4,val5")]
    public void EnumerateRows_WhenRowHasDifferentColumnCount_ThrowsInvalidDataException(string csvContent)
    {
        // Arrange
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var act = () => reader.EnumerateRows(rowsToSkip: 0).ToList();

        // Assert
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void EnumerateRows_WhenMismatchIsBeyondTakenRows_DoesNotThrow()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2\nval3,val4\nval5");
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var rows = reader.EnumerateRows(rowsToSkip: 0).Take(2).ToList();

        // Assert
        rows.Count.Should().Be(2);
    }

    [Theory]
    [InlineData("col1,col2\nv1,v2\nshort\nv3,v4", "v1,v2|v3,v4")]
    [InlineData("col1,col2\nv1,v2\nv9,v9,v9\nv3,v4", "v1,v2|v3,v4")]
    [InlineData("col1,col2\nshort\nlong,long,long\nv1,v2\nx\nv3,v4", "v1,v2|v3,v4")]
    [InlineData("col1,col2\nv1,v2\nshort\n\"p\nq\",v3\nv4,v5", "v1,v2|p\nq,v3|v4,v5")]
    [InlineData("col1,col2\nv1,v2\n\"p\nq\"\nv3,v4", "v1,v2|v3,v4")]
    [InlineData("col1,col2\nv1,v2\nshort", "v1,v2")]
    public void EnumerateRowsSkippingMismatches_SkipsMismatchedRowsAndContinues(
        string csvContent,
        string expected)
    {
        // Arrange
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var rows = reader.EnumerateRowsSkippingMismatches(rowsToSkip: 0).ToList();

        // Assert
        var actual = string.Join('|', rows.Select(r => string.Join(',', ToStringArray(r))));
        actual.Should().Be(expected);
    }

    [Fact]
    public void EnumerateRowsSkippingMismatches_WithRowsToSkip_SkipsDataRows()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nv1,v2\nv3,v4\nbad\nv5,v6");
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var rows = reader.EnumerateRowsSkippingMismatches(rowsToSkip: 2).ToList();

        // Assert
        rows.Count.Should().Be(1);
        ToStringArray(rows[0]).Should().Equal(["v5", "v6"]);
    }

    [Fact]
    public void EnumerateRowsSkippingMismatches_WithSkipCountBeyondDataRows_YieldsNothing()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nv1,v2\nbad");
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var rows = reader.EnumerateRowsSkippingMismatches(rowsToSkip: 5).ToList();

        // Assert
        rows.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateRowsSkippingMismatches_WithEmptyFile_YieldsNothing()
    {
        // Arrange
        File.WriteAllText(_testFilePath, string.Empty);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var rows = reader.EnumerateRowsSkippingMismatches(rowsToSkip: 0).ToList();

        // Assert
        rows.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateRowsSkippingMismatches_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nv1,v2");
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        reader.Dispose();

        // Act
        var act = () => reader.EnumerateRowsSkippingMismatches(rowsToSkip: 0);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void EnumerateRows_WithRowsToSkip_SkipsCorrectNumberOfRows()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2\nval3,val4\nval5,val6";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var rows = reader.EnumerateRows(rowsToSkip: 1).ToList();

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val3", "val4"]);
        ToStringArray(rows[1]).Should().Equal(["val5", "val6"]);
    }

    [Fact]
    public void EnumerateRows_WithSkipCountAtLeastDataRows_YieldsNothing()
    {
        // Arrange - 2 data rows, skip 2 (the exact data row count)
        var csvContent = "col1,col2\nval1,val2\nval3,val4";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var rowsAtExactCount = reader.EnumerateRows(rowsToSkip: 2).ToList();

        // Assert
        rowsAtExactCount.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateRows_WithSkipCountBeyondDataRows_YieldsNothing()
    {
        // Arrange - 2 data rows, skip 100 (beyond the data row count)
        var csvContent = "col1,col2\nval1,val2\nval3,val4";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var rowsBeyondCount = reader.EnumerateRows(rowsToSkip: 100).ToList();

        // Assert
        rowsBeyondCount.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateRows_WithHeaderOnlyFile_YieldsNothing()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\n");
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var rows = reader.EnumerateRows(rowsToSkip: 0).ToList();

        // Assert
        rows.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateRows_WithEmptyFile_YieldsNothing()
    {
        // Arrange - No header row at all
        File.WriteAllText(_testFilePath, string.Empty);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var rows = reader.EnumerateRows(rowsToSkip: 0).ToList();

        // Assert
        rows.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateRows_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2");
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        reader.Dispose();

        // Act
        var act = () => reader.EnumerateRows(rowsToSkip: 0);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void EnumerateRows_WhenDisposedDuringEnumeration_ThrowsObjectDisposedException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2\nval3,val4\nval5,val6");
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        using var enumerator = reader.EnumerateRows(rowsToSkip: 0).GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();

        // Act
        reader.Dispose();
        var act = () => enumerator.MoveNext();

        // Assert - Disposal between yields surfaces on the next MoveNext of the same enumeration
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void EnumerateRowsSkippingMismatches_WhenDisposedDuringEnumeration_ThrowsObjectDisposedException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2\nval3,val4\nval5,val6");
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        using var enumerator = reader.EnumerateRowsSkippingMismatches(rowsToSkip: 0).GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();

        // Act
        reader.Dispose();
        var act = () => enumerator.MoveNext();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }
}

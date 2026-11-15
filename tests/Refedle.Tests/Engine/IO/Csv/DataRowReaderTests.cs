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

    private static List<string> DrainFirstColumns(IEnumerator<CsvDataRow> enumerator)
    {
        var values = new List<string>();
        while (enumerator.MoveNext())
        {
            values.Add(ToStringArray(enumerator.Current)[0]);
        }

        return values;
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
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2);

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
        var rows = reader.ReadRows(byteOffset: -1, rowsToSkip: 0, rowsToRead: 1);

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
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 0);

        // Assert
        rows.Should().BeEmpty();
    }

    [Theory]
    [InlineData("col1,col2,col3\nval1,val2\nval3,val4", 3)]
    [InlineData("col1,col2\nval1,val2,val3\nval4,val5,val6", 2)]
    [InlineData("col1,col2,col3\nval1,val2\nval3,val4,val5", 3)]
    [InlineData("col1,col2\nval1,val2\nval3,val4,val5", 2)]
    [InlineData("col1,col2\nval1,val2\nval3", 2)]
    public void ReadRows_WhenWindowHasRowWithDifferentColumnCount_ThrowsInvalidDataException(
        string csvContent,
        int columnCount)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(csvContent);
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act
        var act = () => reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2);

        // Assert
        act.Should().Throw<InvalidDataException>()
            .WithMessage($"A row does not have the same number of columns as the header ({columnCount}).");
    }

    [Fact]
    public void ReadRows_WhenSepRejectsWindowRow_WrapsSepExceptionAsInner()
    {
        // Arrange - Sep compares each row with the first row it read, so the third row is rejected by Sep
        var csvContent = "col1,col2\nval1,val2\nval3,val4,val5";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act
        var act = () => reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2);

        // Assert
        var exception = act.Should().Throw<InvalidDataException>().Which;
        exception.Message.Should().Be("A row does not have the same number of columns as the header (2).");
        exception.InnerException.Should().BeOfType<InvalidDataException>();
    }

    [Fact]
    public void ReadRows_WhenWindowRowDiffersFromHeaderButMatchesOtherRows_ThrowsWithoutInnerException()
    {
        // Arrange - every row has 3 columns, so Sep is satisfied and the reader's own check rejects them
        var csvContent = "col1,col2\nval1,val2,val3\nval4,val5,val6";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act
        var act = () => reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2);

        // Assert
        act.Should().Throw<InvalidDataException>().Which.InnerException.Should().BeNull();
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
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2);

        // Assert
        rows.Count.Should().Be(2);
    }

    [Theory]
    [InlineData("col1,col2\nshort\nval1,val2\nval3,val4", 1, 2)]
    [InlineData("col1,col2\nx,y,z\nval1,val2\nval3,val4", 1, 2)]
    [InlineData("col1,col2\nval1,val2\nshort\nval3,val4\nval5,val6", 2, 1)]
    [InlineData("col1,col2\nval1,val2\nx,y,z\nval3,val4\nval5,val6", 2, 1)]
    [InlineData("col1,col2\nshort", 1, 1)]
    [InlineData("col1,col2\nval1,val2\nx,y,z", 2, 1)]
    public void ReadRows_WhenSkippedRowHasDifferentColumnCount_ThrowsInvalidDataException(
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
        var act = () => reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip, rowsToRead);

        // Assert
        act.Should().Throw<InvalidDataException>()
            .WithMessage("A row does not have the same number of columns as the header (2).");
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
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2);

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
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 1, rowsToRead: 2);

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
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 100);

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
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 1);

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

    [Fact]
    public void EnumerateRows_WithQuotedCommaEscapedQuoteEmbeddedCrlfAndUnicode_ReturnsExactValues()
    {
        // Arrange - A quoted comma, an escaped quote, an embedded CRLF, and Unicode values
        var csvContent = "col1,col2\n\"a,b\",\"say \"\"hi\"\"\"\n\"line1\r\nline2\",\"日本語\"";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var rows = reader.EnumerateRows(rowsToSkip: 0).ToList();

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["a,b", "say \"hi\""]);
        ToStringArray(rows[1]).Should().Equal(["line1\r\nline2", "日本語"]);
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

    [Theory]
    [InlineData("col1,col2\nv1,v2\nshort\nv3,v4")]
    [InlineData("col1,col2\nv1,v2\nx,y,z\nv3,v4")]
    public void EnumerateRows_WhenSkippedRowHasDifferentColumnCount_ThrowsInvalidDataException(string csvContent)
    {
        // Arrange - The mismatched row is inside the rows-to-skip range
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var act = () => reader.EnumerateRows(rowsToSkip: 2).ToList();

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

    [Theory]
    [InlineData(2)]
    [InlineData(100)]
    public void EnumerateRows_WithSkipCountNotLessThanDataRows_YieldsNothing(int rowsToSkip)
    {
        // Arrange - 2 data rows; the skip count is at (2) or beyond (100) the data row count
        var csvContent = "col1,col2\nval1,val2\nval3,val4";
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act
        var rows = reader.EnumerateRows(rowsToSkip).ToList();

        // Assert
        rows.Should().BeEmpty();
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

    [Theory]
    [InlineData("", 0)]
    [InlineData("col1,col2\n", 0)]
    [InlineData("col1,col2\nval1,val2", 0)]
    [InlineData("col1,col2\nval1,val2", 5)]
    public void EnumerateRows_WhenReaderDisposedBeforeEnumeration_ThrowsObjectDisposedException(
        string csvContent,
        int rowsToSkip)
    {
        // Arrange
        File.WriteAllText(_testFilePath, csvContent);
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        var rows = reader.EnumerateRows(rowsToSkip);

        // Act
        reader.Dispose();
        var act = () => rows.ToList();

        // Assert - Starting the deferred sequence after disposal throws, even when the
        // enumeration would otherwise end without reading a data row
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void EnumerateRows_WhenReaderDisposedAfterFinalRow_NextAdvanceThrowsObjectDisposedException()
    {
        // Arrange - A single data row, already yielded when the reader is disposed
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2");
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        using var enumerator = reader.EnumerateRows(rowsToSkip: 0).GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();

        // Act
        reader.Dispose();
        var act = () => enumerator.MoveNext();

        // Assert - The advance after the final row observes the disposal instead of ending silently
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
    public void EnumerateRows_WhenAdvanceThrowsAfterReaderDispose_ReleasesFileStream()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2\nval3,val4\nval5,val6");
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        using var enumerator = reader.EnumerateRows(rowsToSkip: 0).GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();
        var openExclusively = () =>
        {
            // Dispose an unexpectedly opened stream so a failing check does not leak the handle
            using var opened = new FileStream(_testFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
        };

        // Act - Confirm the active enumeration holds its handle, then dispose the reader and advance
        openExclusively.Should().Throw<IOException>();
        reader.Dispose();
        var advance = () => enumerator.MoveNext();

        // Assert - The exceptional advance itself releases the enumeration's dedicated stream,
        // before any explicit enumerator disposal
        advance.Should().Throw<ObjectDisposedException>();
        openExclusively.Should().NotThrow();
    }

    [Fact]
    public void EnumerateRows_WithInterleavedReaders_PreservesAllRows()
    {
        // Arrange - 1000 distinct rows of ~100 chars, well beyond the reader buffers
        var expectedPadding = new string('x', 90);
        var expectedFirstColumns = Enumerable.Range(1, 1000).Select(static i => $"value{i:D4}").ToList();
        File.WriteAllText(
            _testFilePath,
            "col1,col2\n" + string.Join(
                "\n",
                Enumerable.Range(1, 1000).Select(i => $"value{i:D4},{expectedPadding}")));
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);

        // Act - Start A, interleave ReadRows, then start and advance B before draining A
        using var enumeratorA = reader.EnumerateRows(rowsToSkip: 0).GetEnumerator();
        enumeratorA.MoveNext().Should().BeTrue();
        var aFirst = ToStringArray(enumeratorA.Current)[0];
        var readRowsResult = reader.ReadRows(byteOffset: 0, rowsToSkip: 1, rowsToRead: 2);
        using var enumeratorB = reader.EnumerateRows(rowsToSkip: 500).GetEnumerator();
        enumeratorB.MoveNext().Should().BeTrue();
        var bFirst = ToStringArray(enumeratorB.Current);
        var remainingColumnsFromA = DrainFirstColumns(enumeratorA);
        enumeratorB.MoveNext().Should().BeTrue();
        var bSecond = ToStringArray(enumeratorB.Current);

        // Assert - A yields the exact complete sequence; B continues at its own position
        string[] allColumnsFromA = [aFirst, .. remainingColumnsFromA];
        readRowsResult.Count.Should().Be(2);
        ToStringArray(readRowsResult[0]).Should().Equal(["value0001", expectedPadding]);
        allColumnsFromA.Should().Equal(expectedFirstColumns);
        bFirst.Should().Equal(["value0501", expectedPadding]);
        bSecond.Should().Equal(["value0502", expectedPadding]);
    }

    [Fact]
    public void EnumerateRows_AfterEnumerationCompletes_ReleasesFileStream()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2");
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        var rows = reader.EnumerateRows(rowsToSkip: 0).ToList();
        reader.Dispose();

        // Act - Exclusive access succeeds only when the enumeration's stream is closed too
        using var stream = new FileStream(_testFilePath, FileMode.Open, FileAccess.Read, FileShare.None);

        // Assert
        rows.Count.Should().Be(1);
        stream.Should().NotBeNull();
    }

    [Fact]
    public void EnumerateRows_AfterEnumeratorDisposedMidway_ReleasesFileStream()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2\nval3,val4");
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        using var enumerator = reader.EnumerateRows(rowsToSkip: 0).GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();

        // Act - Abandon the enumeration midway and dispose both the enumerator and the reader
        enumerator.Dispose();
        reader.Dispose();

        // Assert - Exclusive access succeeds only when the abandoned enumeration's stream is closed
        using var stream = new FileStream(_testFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
        stream.Should().NotBeNull();
    }

    [Fact]
    public void EnumerateRows_WithActiveEnumerationAfterReaderDispose_BlocksExclusiveFileAccess()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2\nval3,val4");
        using var reader = new DataRowReader(_testFilePath, columnCount: 2);
        using var enumerator = reader.EnumerateRows(rowsToSkip: 0).GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();

        // Act - Dispose the reader while the enumeration keeps its own handle
        reader.Dispose();
        var act = () =>
        {
            // Dispose an unexpectedly opened stream so a failing check does not leak the handle
            using var opened = new FileStream(_testFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
        };

        // Assert - The active enumeration's dedicated stream is still open
        act.Should().Throw<IOException>();
    }
}

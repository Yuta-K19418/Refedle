using AwesomeAssertions;
using Refedle.App.Tui.Workers.Schema.Csv;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Tui.Workers.Schema.Csv;

public sealed class IncrementalSchemaScannerTests : IDisposable
{
    private readonly string _tempFilePath;

    public IncrementalSchemaScannerTests()
    {
        _tempFilePath = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    [Fact]
    public async Task InitialScanAsync_WithSimpleCsv_ReturnsSchema()
    {
        // Arrange
        string csvContent = "Id,Name,Age\nvalue1,value2,value3\nvalue4,value5,value6";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        IncrementalSchemaScanner scanner = new(_tempFilePath);

        // Act
        TableSchema schema = await scanner.InitialScanAsync();

        // Assert
        schema.Should().NotBeNull();
        schema.ColumnCount.Should().Be(3);
        schema.Columns[0].Name.Should().Be("Id");
        schema.Columns[1].Name.Should().Be("Name");
        schema.Columns[2].Name.Should().Be("Age");
    }

    [Fact]
    public async Task InitialScanAsync_WithHeaderOnlyFile_ReturnsDefaultSchema()
    {
        // Arrange
        string csvContent = "Id,Name,Age\n";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        IncrementalSchemaScanner scanner = new(_tempFilePath);

        // Act
        TableSchema schema = await scanner.InitialScanAsync();

        // Assert
        schema.Should().NotBeNull();
        schema.ColumnCount.Should().Be(3);
        schema.Columns[0].Name.Should().Be("Id");
        schema.Columns[0].Type.Should().Be(ColumnType.Text);
        schema.Columns[0].IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task InitialScanAsync_WithEmptyFile_ThrowsInvalidOperationException()
    {
        // Arrange
        string csvContent = "";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        IncrementalSchemaScanner scanner = new(_tempFilePath);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => scanner.InitialScanAsync());
    }

    [Fact]
    public async Task InitialScanAsync_WithNumericValues_DetectsNumericTypes()
    {
        // Arrange
        string csvContent = "Id,Salary,Bonus\n123,45.6,789\n100,200.5,300";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        IncrementalSchemaScanner scanner = new(_tempFilePath);

        // Act
        TableSchema schema = await scanner.InitialScanAsync();

        // Assert
        schema.Should().NotBeNull();
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[1].Type.Should().Be(ColumnType.FloatingPoint);
        schema.Columns[2].Type.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public async Task InitialScanAsync_WithBooleanValues_DetectsBooleanTypes()
    {
        // Arrange
        string csvContent = "IsActive,IsVerified,IsCompleted\ntrue,false,TRUE\nFALSE,true,False";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        IncrementalSchemaScanner scanner = new(_tempFilePath);

        // Act
        TableSchema schema = await scanner.InitialScanAsync();

        // Assert
        schema.Should().NotBeNull();
        schema.Columns[0].Type.Should().Be(ColumnType.Boolean);
        schema.Columns[1].Type.Should().Be(ColumnType.Boolean);
        schema.Columns[2].Type.Should().Be(ColumnType.Boolean);
    }

    [Fact]
    public async Task InitialScanAsync_WithMixedValues_DetectsTextTypes()
    {
        // Arrange
        string csvContent = "ProductId,Description,Price\n123,text,45.6\ntext,789,text";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        IncrementalSchemaScanner scanner = new(_tempFilePath);

        // Act
        TableSchema schema = await scanner.InitialScanAsync();

        // Assert
        schema.Should().NotBeNull();
        schema.Columns[0].Type.Should().Be(ColumnType.Text);
        schema.Columns[1].Type.Should().Be(ColumnType.Text);
        schema.Columns[2].Type.Should().Be(ColumnType.Text);
    }

    [Fact]
    public async Task InitialScanAsync_WithMissingValues_MarksNullable()
    {
        // Arrange
        string csvContent = "FirstName,MiddleName,LastName\n,,Doe\nJohn,,\n,,Smith";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        IncrementalSchemaScanner scanner = new(_tempFilePath);

        // Act
        TableSchema schema = await scanner.InitialScanAsync();

        // Assert
        schema.Should().NotBeNull();
        schema.Columns[0].IsNullable.Should().BeTrue();
        schema.Columns[1].IsNullable.Should().BeTrue();
        schema.Columns[2].IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task InitialScanAsync_Constructor_WithNonexistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        string nonexistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".csv");
        IncrementalSchemaScanner scanner = new(nonexistentPath);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => scanner.InitialScanAsync());
    }

    [Fact]
    public async Task StartBackgroundScanAsync_WithAlreadyCancelledToken_ReturnsCanceledTask()
    {
        // Arrange
        string csvContent = "Id,Name,Age\nvalue1,value2,value3\nvalue4,value5,value6";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        IncrementalSchemaScanner scanner = new(_tempFilePath);
        TableSchema schema = await scanner.InitialScanAsync();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        Task<TableSchema> backgroundTask = scanner.StartBackgroundScanAsync(schema, cts.Token);
        await Task.WhenAny(backgroundTask);

        // Assert
        backgroundTask.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task InitialScanAsync_WithQuotedHeaderAndQuotedNumber_ReadsWithoutQuotes()
    {
        // Arrange
        // Header: "say ""hi""",Count
        string headerLine = "\"say \"\"hi\"\"\",Count";
        // Data: value1,"12"
        string dataLine = "value1,\"12\"";
        string[] lines = [headerLine, dataLine];
        string csvContent = string.Join("\n", lines);
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        IncrementalSchemaScanner scanner = new(_tempFilePath);

        // Act
        TableSchema schema = await scanner.InitialScanAsync();

        // Assert
        // Expected: column 1 name is say "hi", column 2 type is WholeNumber
        schema.Columns[0].Name.Should().Be("say \"hi\"");
        schema.Columns[1].Type.Should().Be(ColumnType.WholeNumber);
    }

    [Theory]
    [InlineData("Id,Age\n1,20\n2")]
    [InlineData("Id,Age\n1,20\n2,text,excess")]
    public async Task InitialScanAsync_WithMismatchedColumnCountInInitialRows_ThrowsInvalidDataException(
        string csvContent)
    {
        // Arrange
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        IncrementalSchemaScanner scanner = new(_tempFilePath);

        // Act
        Func<Task<TableSchema>> act = async () => await scanner.InitialScanAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Theory]
    [InlineData("2")]
    [InlineData("2,text,excess")]
    public async Task InitialScanAsync_WithMismatchedRowAtInitialScanCount_ThrowsInvalidDataException(
        string mismatchedRow)
    {
        // Arrange - 199 valid rows, then a mismatched row exactly at the 200th (InitialScanCount) row
        IEnumerable<string> validRows = Enumerable.Repeat("1,20", 199);
        await File.WriteAllLinesAsync(_tempFilePath, validRows.Append(mismatchedRow).Prepend("Id,Age"));
        IncrementalSchemaScanner scanner = new(_tempFilePath);

        // Act
        Func<Task<TableSchema>> act = async () => await scanner.InitialScanAsync();

        // Assert - The 200th row is inside the initial scan's window
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task StartBackgroundScanAsync_WithValidRowsAfterInitialRows_RefinesSchema()
    {
        // Arrange - 200 rows for the initial scan, then a row with text to widen the schema
        IEnumerable<string> initialRows = Enumerable.Repeat("1,20", 200);
        string[] laterRows = ["3,text"];
        await File.WriteAllLinesAsync(_tempFilePath, initialRows.Concat(laterRows).Prepend("Id,Age"));
        IncrementalSchemaScanner scanner = new(_tempFilePath);
        TableSchema schema = await scanner.InitialScanAsync();
        schema.Columns[1].Type.Should().Be(ColumnType.WholeNumber);

        // Act
        TableSchema refinedSchema = await scanner.StartBackgroundScanAsync(schema, CancellationToken.None);

        // Assert - The row beyond the initial scan refines Age to Text
        refinedSchema.Columns[1].Type.Should().Be(ColumnType.Text);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("2,text,excess")]
    public async Task StartBackgroundScanAsync_WithMismatchedFirstBackgroundRow_KeepsInitialSchema(
        string mismatchedRow)
    {
        // Arrange - exactly 200 valid rows (initial scan), a mismatched first background row,
        // then a row that would change the types if the scan kept reading
        IEnumerable<string> initialRows = Enumerable.Repeat("1,20", 200);
        string[] laterRows = [mismatchedRow, "abc,text"];
        await File.WriteAllLinesAsync(_tempFilePath, initialRows.Concat(laterRows).Prepend("Id,Age"));
        IncrementalSchemaScanner scanner = new(_tempFilePath);
        TableSchema schema = await scanner.InitialScanAsync();

        // Act
        TableSchema refinedSchema = await scanner.StartBackgroundScanAsync(schema, CancellationToken.None);

        // Assert - The scan succeeds and the schema is the one from the initial scan
        refinedSchema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        refinedSchema.Columns[0].IsNullable.Should().BeFalse();
        refinedSchema.Columns[1].Type.Should().Be(ColumnType.WholeNumber);
        refinedSchema.Columns[1].IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task StartBackgroundScanAsync_WhenFileIsDeletedAfterInitialScan_PropagatesFileNotFoundException()
    {
        // Arrange
        IEnumerable<string> rows = Enumerable.Repeat("1,20", 250).Prepend("Id,Age");
        await File.WriteAllLinesAsync(_tempFilePath, rows);
        IncrementalSchemaScanner scanner = new(_tempFilePath);
        TableSchema schema = await scanner.InitialScanAsync();
        File.Delete(_tempFilePath);

        // Act
        Func<Task<TableSchema>> act = async () => await scanner.StartBackgroundScanAsync(schema, CancellationToken.None);

        // Assert - Only a column-count mismatch ends the scan quietly; I/O errors still propagate
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Theory]
    [InlineData("2")]
    [InlineData("2,text,excess")]
    public async Task StartBackgroundScanAsync_WithMismatchedRowAfterInitialRows_StopsAndKeepsSchemaFromEarlierRows(
        string mismatchedRow)
    {
        // Arrange - 200 normal rows (initial scan), a text row that widens Age, a mismatched row,
        // then a row that would widen Id if the scan kept reading
        IEnumerable<string> initialRows = Enumerable.Repeat("1,20", 200);
        string[] laterRows = ["3,text", mismatchedRow, "abc,5"];
        await File.WriteAllLinesAsync(_tempFilePath, initialRows.Concat(laterRows).Prepend("Id,Age"));
        IncrementalSchemaScanner scanner = new(_tempFilePath);
        TableSchema schema = await scanner.InitialScanAsync();
        schema.Columns[1].Type.Should().Be(ColumnType.WholeNumber);

        // Act
        TableSchema refinedSchema = await scanner.StartBackgroundScanAsync(schema, CancellationToken.None);

        // Assert - The scan ends at the mismatched row without faulting; rows after it are not reflected
        refinedSchema.Columns[1].Type.Should().Be(ColumnType.Text);
        refinedSchema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        refinedSchema.Columns[0].IsNullable.Should().BeFalse();
    }
}

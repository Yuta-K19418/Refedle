using AwesomeAssertions;
using Refedle.App.Tui.Workers.Schema.Csv;
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
        var csvContent = "Id,Name,Age\nvalue1,value2,value3\nvalue4,value5,value6";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act
        var schema = await scanner.InitialScanAsync();

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
        var csvContent = "Id,Name,Age\n";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act
        var schema = await scanner.InitialScanAsync();

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
        var csvContent = "";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => scanner.InitialScanAsync());
    }

    [Fact]
    public async Task InitialScanAsync_WithNumericValues_DetectsNumericTypes()
    {
        // Arrange
        var csvContent = "Id,Salary,Bonus\n123,45.6,789\n100,200.5,300";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act
        var schema = await scanner.InitialScanAsync();

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
        var csvContent = "IsActive,IsVerified,IsCompleted\ntrue,false,TRUE\nFALSE,true,False";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act
        var schema = await scanner.InitialScanAsync();

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
        var csvContent = "ProductId,Description,Price\n123,text,45.6\ntext,789,text";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act
        var schema = await scanner.InitialScanAsync();

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
        var csvContent = "FirstName,MiddleName,LastName\n,,Doe\nJohn,,\n,,Smith";
        await File.WriteAllTextAsync(_tempFilePath, csvContent);
        var scanner = new IncrementalSchemaScanner(_tempFilePath);

        // Act
        var schema = await scanner.InitialScanAsync();

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
        var nonexistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".csv");
        var scanner = new IncrementalSchemaScanner(nonexistentPath);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => scanner.InitialScanAsync());
    }

    [Fact]
    public async Task Dispose_CancelsBackgroundScanAsync()
    {
        // Arrange
        var csvContent = "Id,Name,Age\nvalue1,value2,value3\nvalue4,value5,value6\nvalue7,value8,value9";
        File.WriteAllText(_tempFilePath, csvContent);

        var scanner = new IncrementalSchemaScanner(_tempFilePath);
        var schema = await scanner.InitialScanAsync();

        // Act
        using var cts = new CancellationTokenSource();
        var backgroundTask = scanner.StartBackgroundScanAsync(schema, cts.Token);
        cts.Cancel();

        // Assert
        // Wait deterministically for the cancellation to propagate (up to 5 seconds)
        await Task.WhenAny(backgroundTask, Task.Delay(TimeSpan.FromSeconds(5)));
        backgroundTask.IsCompleted.Should().BeTrue();
    }
}

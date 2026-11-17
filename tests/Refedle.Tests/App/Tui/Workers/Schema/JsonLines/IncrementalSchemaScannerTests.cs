using AwesomeAssertions;
using Refedle.App.Tui.Workers.Schema.JsonLines;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Tui.Workers.Schema.JsonLines;

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
    public async Task InitialScanAsync_WithValidFile_ReturnsSchemaWithCorrectColumns()
    {
        // Arrange
        await File.WriteAllLinesAsync(
            _tempFilePath,
            ["{\"id\":1,\"name\":\"Alice\"}", "{\"id\":2,\"name\":\"Bob\"}"]
        );
        IncrementalSchemaScanner scanner = new(_tempFilePath);

        // Act
        TableSchema schema = await scanner.InitialScanAsync();

        // Assert
        schema.ColumnCount.Should().Be(2);
        schema.Columns[0].Name.Should().Be("id");
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[1].Name.Should().Be("name");
        schema.Columns[1].Type.Should().Be(ColumnType.Text);
    }

    [Fact]
    public async Task InitialScanAsync_WithEmptyFile_ThrowsInvalidOperationException()
    {
        // Arrange
        await File.WriteAllTextAsync(_tempFilePath, string.Empty);
        IncrementalSchemaScanner scanner = new(_tempFilePath);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => scanner.InitialScanAsync());
    }

    [Fact]
    public async Task StartBackgroundScanAsync_NewColumnDiscovered_RefinesSchemaWithNullableColumn()
    {
        // Arrange
        // 200 lines with only "a", then 1 line introducing "b"
        IEnumerable<string> initialLines = Enumerable.Repeat("{\"a\":1}", 200);
        string[] extraLine = ["{\"a\":2,\"b\":\"extra\"}"];
        await File.WriteAllLinesAsync(_tempFilePath, initialLines.Concat(extraLine));
        IncrementalSchemaScanner scanner = new(_tempFilePath);
        TableSchema schema = await scanner.InitialScanAsync();

        // Act
        TableSchema refinedSchema = await scanner.StartBackgroundScanAsync(schema, CancellationToken.None);

        // Assert
        refinedSchema.ColumnCount.Should().Be(2);
        refinedSchema.Columns[1].Name.Should().Be("b");
        refinedSchema.Columns[1].IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task StartBackgroundScanAsync_WithAlreadyCancelledToken_ReturnsCanceledTask()
    {
        // Arrange
        await File.WriteAllLinesAsync(_tempFilePath, ["{\"id\":1}"]);
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
    public async Task StartBackgroundScanAsync_NoRemainingLines_ReturnsOriginalSchemaUnchanged()
    {
        // Arrange
        // Fewer than 200 lines so background scan finds nothing at offset 200+
        await File.WriteAllLinesAsync(
            _tempFilePath,
            Enumerable.Range(0, 5).Select(i => $"{{\"id\":{i}}}")
        );
        IncrementalSchemaScanner scanner = new(_tempFilePath);
        TableSchema schema = await scanner.InitialScanAsync();

        // Act
        TableSchema refinedSchema = await scanner.StartBackgroundScanAsync(schema, CancellationToken.None);

        // Assert
        refinedSchema.ColumnCount.Should().Be(schema.ColumnCount);
        refinedSchema.Columns[0].Name.Should().Be(schema.Columns[0].Name);
    }
}

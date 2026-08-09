using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.Engine;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Cli;

public sealed class JsonLinesRecordReaderTests
{
    private const string ColumnName = "value";

    [Fact]
    public async Task GetCellData_NumberToken_ReturnsValueRaw()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"value":1.50}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputSchema, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Value);
            cell.Encoding.Should().Be(CellEncoding.Raw);
            cell.Value.ToString().Should().Be("1.50");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_ObjectToken_ReturnsValueRaw()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"value":{"a":1}}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputSchema, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Value);
            cell.Encoding.Should().Be(CellEncoding.Raw);
            cell.Value.ToString().Should().Be("""{"a":1}""");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_ArrayToken_ReturnsValueRaw()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"value":[1,2,3]}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputSchema, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Value);
            cell.Encoding.Should().Be(CellEncoding.Raw);
            cell.Value.ToString().Should().Be("[1,2,3]");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_StringToken_ReturnsValuePlainText()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"value":"hello"}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputSchema, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Value);
            cell.Encoding.Should().Be(CellEncoding.PlainText);
            cell.Value.ToString().Should().Be("hello");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_BooleanToken_ReturnsValueBoolean()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"value":true}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputSchema, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Value);
            cell.Encoding.Should().Be(CellEncoding.Boolean);
            cell.Value.ToString().Should().Be("true");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_FalseToken_ReturnsValueBoolean()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"value":false}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputSchema, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Value);
            cell.Encoding.Should().Be(CellEncoding.Boolean);
            cell.Value.ToString().Should().Be("false");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_NullToken_ReturnsNullPresence()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"value":null}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputSchema, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Null);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_MissingProperty_ReturnsMissingPresence()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"other":1}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputSchema, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Missing);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetCellData_MalformedJson_ReturnsInvalidPresence()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, "not valid json");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputSchema, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var cell = reader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Invalid);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void GetCellData_MultipleColumnsReadSequentially_KeepsEachValueValidWhenRead()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void GetCellData_StringExceedingInitialBuffer_ReturnsFullValue()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void GetCellData_StringWithEscapeSequences_ReturnsResolvedText()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void GetCellData_EmptyString_ReturnsEmptyValueWithoutError()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void GetCellData_AfterDisposingOriginalCopy_ThrowsObjectDisposedException()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void GetCellData_AfterWarmUp_AllocatesZeroBytes()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    private static (TableSchema InputSchema, BatchOutputSchema OutputSchema) BuildSchemas()
    {
        var inputSchema = new TableSchema
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [new ColumnSchema { Name = ColumnName, Type = ColumnType.Text, ColumnIndex = 0 }],
        };
        var outputSchema = new BatchOutputSchema([new BatchOutputColumn(ColumnName, ColumnName)], []);
        return (inputSchema, outputSchema);
    }
}

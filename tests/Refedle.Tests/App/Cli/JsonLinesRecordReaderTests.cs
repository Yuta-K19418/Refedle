using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.Engine;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Cli;

public sealed partial class JsonLinesRecordReaderTests
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
    public void ThrowIfDisposed_AfterDispose_ThrowsWithConcreteTypeName()
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
            var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            reader.Dispose();

            // Act
            Action act = () => reader.ThrowIfDisposed();

            // Assert
            var exception = act.Should().Throw<ObjectDisposedException>().Which;
            exception.ObjectName.Should().Be(typeof(JsonLinesRecordReader).FullName);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    // JSON null extracts to the "<null>" sentinel, which the reader rejects.
    [Fact]
    public async Task EvaluateFilters_NullJsonValue_ReturnsFalse()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"value":null}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            BatchFilterSpec[] filters = [new(0, ComparisonType.Text, FilterOperator.Equals, "anything")];
            var (inputSchema, outputSchema) = BuildSchemas([ColumnName], filters);
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var result = reader.EvaluateFilters();

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    // A non-object line extracts to the "<error>" sentinel, which the reader rejects.
    [Fact]
    public async Task EvaluateFilters_MalformedJsonValue_ReturnsFalse()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, "not valid json");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            BatchFilterSpec[] filters = [new(0, ComparisonType.Text, FilterOperator.Equals, "anything")];
            var (inputSchema, outputSchema) = BuildSchemas([ColumnName], filters);
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var result = reader.EvaluateFilters();

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    // A filter whose source column index is absent from the input schema is ignored, not rejected.
    [Fact]
    public async Task EvaluateFilters_FilterColumnAbsentFromInputSchema_IgnoresFilter()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"value":1}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            BatchFilterSpec[] filters = [new(9, ComparisonType.Text, FilterOperator.Equals, "anything")];
            var (inputSchema, outputSchema) = BuildSchemas([ColumnName], filters);
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var result = reader.EvaluateFilters();

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task EvaluateFilters_MatchingStringFilter_ReturnsTrue()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, """{"value":"hello"}""");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            BatchFilterSpec[] filters = [new(0, ComparisonType.Text, FilterOperator.Equals, "hello")];
            var (inputSchema, outputSchema) = BuildSchemas([ColumnName], filters);
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);
            await reader.MoveNextAsync(default);

            // Act
            var result = reader.EvaluateFilters();

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    // Leading empty and whitespace-only lines are skipped before the first JSON object,
    // exercising StringUtility.IsWhiteSpace at its production call site.
    [Fact]
    public async Task MoveNextAsync_LeadingEmptyAndWhitespaceLines_SkipsToFirstObject()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, "\n \t\n{\"value\":1}\n");
            var rowIndexer = new RowIndexer(filePath);
            rowIndexer.BuildIndex();
            using var rowReader = new RowReader(filePath);
            var (inputSchema, outputSchema) = BuildSchemas();
            using var reader = new JsonLinesRecordReader(rowIndexer, rowReader, inputSchema, outputSchema);

            // Act
            var hasObject = await reader.MoveNextAsync(default);

            // Assert
            hasObject.Should().BeTrue();
            reader.GetCellData(0).Value.ToString().Should().Be("1");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static (TableSchema InputSchema, BatchOutputSchema OutputSchema) BuildSchemas() =>
        BuildSchemas([ColumnName]);

    private static (TableSchema InputSchema, BatchOutputSchema OutputSchema) BuildSchemas(IReadOnlyList<string> columnNames)
        => BuildSchemas(columnNames, []);

    private static (TableSchema InputSchema, BatchOutputSchema OutputSchema) BuildSchemas(
        IReadOnlyList<string> columnNames,
        IReadOnlyList<BatchFilterSpec> filters)
    {
        var inputSchema = new TableSchema
        {
            SourceFormat = DataFormat.JsonLines,
            Columns = [.. columnNames.Select((name, index) => new ColumnSchema { Name = name, Type = ColumnType.Text, ColumnIndex = index })],
        };
        var outputSchema = new BatchOutputSchema([.. columnNames.Select(name => new BatchOutputColumn(name, name))], filters);
        return (inputSchema, outputSchema);
    }
}

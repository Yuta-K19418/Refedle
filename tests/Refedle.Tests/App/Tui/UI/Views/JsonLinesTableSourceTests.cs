using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;
using Refedle.Engine.Types;
using Refedle.TestHelpers.IO;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed class JsonLinesTableSourceTests : IDisposable
{
    private readonly TempFile _tempFile = TempFile.CreateEmpty(".tmp");

    private readonly string _testFilePath;

    private readonly RowIndexer _indexer;

    public JsonLinesTableSourceTests()
    {
        // xUnit does not dispose a fixture whose constructor throws, so release the temp file here.
        try
        {
            _testFilePath = _tempFile.Path;
            File.WriteAllLines(
                _testFilePath,
                ["{\"id\":1,\"name\":\"Alice\"}", "{\"id\":2,\"name\":\"Bob\"}"]
            );
            _indexer = new RowIndexer(_testFilePath);
            _indexer.BuildIndex();
        }
        catch
        {
            _tempFile.Dispose();
            throw;
        }
    }

    public void Dispose() => _tempFile.Dispose();

    [Fact]
    public void Indexer_ExistingKey_ReturnsCellValue()
    {
        // Arrange
        using RowByteCache cache = new(_indexer);
        TableSchema schema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "id",
                    Type = ColumnType.WholeNumber,
                    ColumnIndex = 0,
                },
                new ColumnSchema
                {
                    Name = "name",
                    Type = ColumnType.Text,
                    ColumnIndex = 1,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        using JsonLinesTableSource source = new(cache, schema);

        // Act
        object result = source[0, 1]; // row 0, col 1 ("name")

        // Assert
        result.Should().Be("Alice");
    }

    [Fact]
    public void Indexer_MissingKey_ReturnsNullPlaceholder()
    {
        // Arrange
        using RowByteCache cache = new(_indexer);
        TableSchema schema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "id",
                    Type = ColumnType.WholeNumber,
                    ColumnIndex = 0,
                },
                new ColumnSchema
                {
                    Name = "name",
                    Type = ColumnType.Text,
                    ColumnIndex = 1,
                },
                new ColumnSchema
                {
                    Name = "email",
                    Type = ColumnType.Text,
                    ColumnIndex = 2,
                    IsNullable = true,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        using JsonLinesTableSource source = new(cache, schema);

        // Act
        object result = source[0, 2]; // "email" does not exist in the JSON line

        // Assert
        result.Should().Be("<null>");
    }

    [Fact]
    public void UpdateSchema_NewColumnAdded_IncreasesColumnCount()
    {
        // Arrange
        using RowByteCache cache = new(_indexer);
        TableSchema originalSchema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "id",
                    Type = ColumnType.WholeNumber,
                    ColumnIndex = 0,
                },
                new ColumnSchema
                {
                    Name = "name",
                    Type = ColumnType.Text,
                    ColumnIndex = 1,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        TableSchema refinedSchema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "id",
                    Type = ColumnType.WholeNumber,
                    ColumnIndex = 0,
                },
                new ColumnSchema
                {
                    Name = "name",
                    Type = ColumnType.Text,
                    ColumnIndex = 1,
                },
                new ColumnSchema
                {
                    Name = "email",
                    Type = ColumnType.Text,
                    ColumnIndex = 2,
                    IsNullable = true,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        using JsonLinesTableSource source = new(cache, originalSchema);

        // Act
        source.UpdateSchema(refinedSchema);

        // Assert
        source.Columns.Should().Be(3);
        source.ColumnNames[2].Should().Be("email (text)");
    }

    [Fact]
    public void Indexer_NegativeRow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using RowByteCache cache = new(_indexer);
        TableSchema schema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "id",
                    Type = ColumnType.WholeNumber,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        using JsonLinesTableSource source = new(cache, schema);

        // Act
        Func<object> act = () => _ = source[-1, 0];

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_NegativeCol_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using RowByteCache cache = new(_indexer);
        TableSchema schema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "id",
                    Type = ColumnType.WholeNumber,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        using JsonLinesTableSource source = new(cache, schema);

        // Act
        Func<object> act = () => _ = source[0, -1];

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_RowExceedsBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using RowByteCache cache = new(_indexer);
        TableSchema schema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "id",
                    Type = ColumnType.WholeNumber,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        using JsonLinesTableSource source = new(cache, schema);

        // Act
        Func<object> act = () => _ = source[2, 0]; // Only 2 rows (index 0 and 1), so row 2 is out of range

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_ColExceedsBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using RowByteCache cache = new(_indexer);
        TableSchema schema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "id",
                    Type = ColumnType.WholeNumber,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        using JsonLinesTableSource source = new(cache, schema);

        // Act
        Func<object> act = () => _ = source[0, 1]; // Only 1 column (index 0), so col 1 is out of range

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rows_ReflectsCacheLineCount()
    {
        // Arrange
        using RowByteCache cache = new(_indexer);
        TableSchema schema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "id",
                    Type = ColumnType.WholeNumber,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        using JsonLinesTableSource source = new(cache, schema);

        // Act
        long rows = source.TotalRows;

        // Assert
        rows.Should().Be(2); // Two lines written in test setup
    }

    [Fact]
    public void Dispose_DisposesRowByteCache()
    {
        // Arrange
        RowByteCache cache = new(_indexer);
        TableSchema schema = new()
        {
            Columns = [new ColumnSchema { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 }],
            SourceFormat = DataFormat.JsonLines,
        };
        JsonLinesTableSource source = new(cache, schema);
        _ = source[0, 0]; // Ensure MmapService is initialized

        // Act
        source.Dispose();

        // Assert
        // Verify indexer throws ObjectDisposedException
        Func<object> act = () => _ = source[0, 0];
        act.Should().Throw<ObjectDisposedException>();
    }
}

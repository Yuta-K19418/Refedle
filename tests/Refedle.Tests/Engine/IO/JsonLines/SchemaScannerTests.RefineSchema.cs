using AwesomeAssertions;
using Refedle.Engine;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed partial class SchemaScannerTests
{
    [Fact]
    public void RefineSchema_NewKeyInLine_AddsNullableColumn()
    {
        // Arrange
        TableSchema initialSchema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "a",
                    Type = ColumnType.WholeNumber,
                    IsNullable = false,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        JsonRawBytes line = Line("{\"a\": 1, \"b\": \"new\"}");

        // Act
        Result<TableSchema> result = SchemaScanner.RefineSchema(initialSchema, line.Span);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Should().HaveCount(2);
        AssertColumn(result.Value, 0, "a", ColumnType.WholeNumber, false);
        AssertColumn(result.Value, 1, "b", ColumnType.Text, true);
    }

    [Fact]
    public void RefineSchema_ExistingColumnAbsentInLine_MarksNullable()
    {
        // Arrange
        TableSchema initialSchema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "a",
                    Type = ColumnType.WholeNumber,
                    IsNullable = false,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        JsonRawBytes line = Line("{}"); // 'a' is missing

        // Act
        Result<TableSchema> result = SchemaScanner.RefineSchema(initialSchema, line.Span);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", ColumnType.WholeNumber, true);
    }

    [Fact]
    public void RefineSchema_TypeConflict_ResolvesViaColumnTypeResolver()
    {
        // Arrange
        TableSchema initialSchema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "a",
                    Type = ColumnType.WholeNumber,
                    IsNullable = false,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        JsonRawBytes line = Line("{\"a\": 1.5}"); // Conflict: WholeNumber vs FloatingPoint

        // Act
        Result<TableSchema> result = SchemaScanner.RefineSchema(initialSchema, line.Span);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", ColumnType.FloatingPoint, false);
    }

    [Fact]
    public void RefineSchema_MalformedJsonLine_ReturnsOriginalSchemaUnchanged()
    {
        // Arrange
        TableSchema initialSchema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "a",
                    Type = ColumnType.WholeNumber,
                    IsNullable = false,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };
        JsonRawBytes line = new("malformed"u8.ToArray());

        // Act
        Result<TableSchema> result = SchemaScanner.RefineSchema(initialSchema, line.Span);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(initialSchema);
    }

    [Fact]
    public void RefineSchema_EmptySpan_ReturnsOriginalSchemaUnchanged()
    {
        // Arrange
        TableSchema initialSchema = new()
        {
            Columns =
            [
                new ColumnSchema
                {
                    Name = "a",
                    Type = ColumnType.WholeNumber,
                    IsNullable = false,
                    ColumnIndex = 0,
                },
            ],
            SourceFormat = DataFormat.JsonLines,
        };

        // Act
        Result<TableSchema> result = SchemaScanner.RefineSchema(initialSchema, []);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(initialSchema);
    }
}

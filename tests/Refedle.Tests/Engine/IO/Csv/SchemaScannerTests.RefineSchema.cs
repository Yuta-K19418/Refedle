using AwesomeAssertions;
using Refedle.Engine;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.Csv;

public sealed partial class SchemaScannerTests
{
    [Fact]
    public void RefineSchema_CallsMarkNullableOnly_WhenNonNullableColumnHasEmptyValue()
    {
        // Arrange
        TableSchema schema = CreateTestSchema([
            new ColumnSchema
            {
                Name = "id",
                Type = ColumnType.WholeNumber,
                IsNullable = false,
                ColumnIndex = 0,
            },
        ]);

        ReadOnlyMemory<char>[] row = ["".AsMemory()];

        // Act
        Result<TableSchema> result = SchemaScanner.RefineSchema(schema, row);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        result.Value.Columns[0].IsNullable.Should().BeTrue();
    }

    [Fact]
    public void RefineSchema_MismatchedColumnCount_ReturnsFailure()
    {
        // Arrange
        TableSchema schema = CreateTestSchema([
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
        ]);

        ReadOnlyMemory<char>[] row = ["123".AsMemory()];

        // Act
        Result<TableSchema> result = SchemaScanner.RefineSchema(schema, row);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Row has 1 columns but schema has 2 columns");
    }

    [Fact]
    public void RefineSchema_NullSchema_ThrowsException()
    {
        // Arrange
        ReadOnlyMemory<char>[] row = ["123".AsMemory()];

        // Act & Assert
        Action action = () => SchemaScanner.RefineSchema(null!, row);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RefineSchema_NullRow_ThrowsException()
    {
        // Arrange
        TableSchema schema = CreateTestSchema([
            new ColumnSchema
            {
                Name = "id",
                Type = ColumnType.WholeNumber,
                ColumnIndex = 0,
            },
        ]);

        // Act & Assert
        Action action = () => SchemaScanner.RefineSchema(schema, null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RefineSchema_WithNoChanges_ReturnsSameInstance()
    {
        // Arrange
        TableSchema schema = CreateTestSchema([
            new ColumnSchema
            {
                Name = "id",
                Type = ColumnType.WholeNumber,
                IsNullable = true,
                ColumnIndex = 0,
            },
        ]);

        ReadOnlyMemory<char>[] row = ["123".AsMemory()];

        // Act
        Result<TableSchema> result = SchemaScanner.RefineSchema(schema, row);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should return same instance when schema not modified (copy-on-write optimization)
        result.Value.Should().BeSameAs(schema);
        result.Value.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        result.Value.Columns[0].IsNullable.Should().BeTrue();
    }

    [Fact]
    public void RefineSchema_WithNullableUpdate_ReturnsNewInstance()
    {
        // Arrange
        TableSchema schema = CreateTestSchema([
            new ColumnSchema
            {
                Name = "id",
                Type = ColumnType.WholeNumber,
                IsNullable = false,
                ColumnIndex = 0,
            },
        ]);

        ReadOnlyMemory<char>[] row = ["".AsMemory()]; // empty value

        // Act
        Result<TableSchema> result = SchemaScanner.RefineSchema(schema, row);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeSameAs(schema); // new instance returned
        result.Value.Columns[0].Type.Should().Be(ColumnType.WholeNumber); // type unchanged
        result.Value.Columns[0].IsNullable.Should().BeTrue(); // only nullable updated
    }

    [Fact]
    public void RefineSchema_WithTypeUpdate_ReturnsNewInstance()
    {
        // Arrange
        TableSchema schema = CreateTestSchema([
            new ColumnSchema
            {
                Name = "id",
                Type = ColumnType.WholeNumber, // initial value is WholeNumber
                IsNullable = false,
                ColumnIndex = 0,
            },
        ]);

        ReadOnlyMemory<char>[] row = ["123.45".AsMemory()]; // parsable as floating point

        // Act
        Result<TableSchema> result = SchemaScanner.RefineSchema(schema, row);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeSameAs(schema); // new instance returned
        result.Value.Columns[0].Type.Should().Be(ColumnType.FloatingPoint); // WholeNumber → FloatingPoint updated
        result.Value.Columns[0].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void RefineSchema_PartialColumnUpdates_PreservesUnchangedColumns()
    {
        // Arrange
        TableSchema schema = CreateTestSchema([
            new ColumnSchema
            {
                Name = "id",
                Type = ColumnType.WholeNumber,
                IsNullable = false,
                ColumnIndex = 0,
            },
            new ColumnSchema
            {
                Name = "name",
                Type = ColumnType.Text,
                IsNullable = false,
                ColumnIndex = 1,
            },
            new ColumnSchema
            {
                Name = "age",
                Type = ColumnType.WholeNumber,
                IsNullable = false,
                ColumnIndex = 2,
            },
        ]);

        ReadOnlyMemory<char>[] row =
        [
            "123".AsMemory(), // id - unchanged (WholeNumber)
            "".AsMemory(), // name - updated to nullable
            "25.5".AsMemory(), // age - updated to FloatingPoint
        ];

        // Act
        Result<TableSchema> result = SchemaScanner.RefineSchema(schema, row);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeSameAs(schema);

        // Column 0: unchanged
        result.Value.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        result.Value.Columns[0].IsNullable.Should().BeFalse();

        // Column 1: only nullable updated
        result.Value.Columns[1].Type.Should().Be(ColumnType.Text);
        result.Value.Columns[1].IsNullable.Should().BeTrue();

        // Column 2: only type updated
        result.Value.Columns[2].Type.Should().Be(ColumnType.FloatingPoint);
        result.Value.Columns[2].IsNullable.Should().BeFalse();
    }

    private static TableSchema CreateTestSchema(ColumnSchema[] columns)
    {
        return new TableSchema { Columns = columns, SourceFormat = DataFormat.Csv };
    }
}

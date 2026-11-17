using System.Globalization;
using AwesomeAssertions;
using Refedle.Engine;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.Csv;

public sealed partial class SchemaScannerTests
{
    [Fact]
    public void ScanSchema_SimpleCsvWithMixedTypes_ReturnsCorrectSchema()
    {
        // Arrange
        string[] columnNames = ["id", "name", "age", "salary", "active", "created_at"];
        ReadOnlyMemory<char>[] row =
        [
            "1".AsMemory(),
            "Alice".AsMemory(),
            "30".AsMemory(),
            "50000.50".AsMemory(),
            "true".AsMemory(),
            "2024-01-15".AsMemory(),
        ];
        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        TableSchema schema = result.Value;
        schema.ColumnCount.Should().Be(6);
        schema.SourceFormat.Should().Be(DataFormat.Csv);

        // Check each column
        AssertColumn(schema, 0, "id", ColumnType.WholeNumber, false);
        AssertColumn(schema, 1, "name", ColumnType.Text, false);
        AssertColumn(schema, 2, "age", ColumnType.WholeNumber, false);
        AssertColumn(schema, 3, "salary", ColumnType.FloatingPoint, false);
        AssertColumn(schema, 4, "active", ColumnType.Boolean, false);
        AssertColumn(schema, 5, "created_at", ColumnType.Timestamp, false);
    }

    [Fact]
    public void ScanSchema_WithNullableColumns_MarksNullableCorrectly()
    {
        // Arrange
        string[] columnNames = ["id", "name", "optional_field"];
        ReadOnlyMemory<char>[] row =
        [
            "1".AsMemory(),
            "Alice".AsMemory(),
            "".AsMemory(), // Empty value
        ];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        TableSchema schema = result.Value;

        schema.Columns[0].Name.Should().Be("id");
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("name");
        schema.Columns[1].Type.Should().Be(ColumnType.Text);
        schema.Columns[1].IsNullable.Should().BeFalse();

        schema.Columns[2].Name.Should().Be("optional_field");
        schema.Columns[2].Type.Should().Be(ColumnType.Text);
        schema.Columns[2].IsNullable.Should().BeTrue(); // Nullable because empty
    }

    [Fact]
    public void ScanSchema_WithWhitespaceOnlyValue_MarksNullable()
    {
        // Arrange
        string[] columnNames = ["id", "space_field", "tab_field"];
        ReadOnlyMemory<char>[] row =
        [
            "1".AsMemory(),
            "   ".AsMemory(), // Whitespace-only
            "\t\n\r ".AsMemory(), // Various whitespace characters
        ];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        TableSchema schema = result.Value;

        schema.Columns[0].Name.Should().Be("id");
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("space_field");
        schema.Columns[1].Type.Should().Be(ColumnType.Text);
        schema.Columns[1].IsNullable.Should().BeTrue(); // Nullable

        schema.Columns[2].Name.Should().Be("tab_field");
        schema.Columns[2].Type.Should().Be(ColumnType.Text);
        schema.Columns[2].IsNullable.Should().BeTrue(); // Nullable
    }

    [Fact]
    public void ScanSchema_EmptyColumnNames_GeneratesColumnNames()
    {
        // Arrange
        string[] columnNames = ["", "  ", "name"]; // First two are empty/whitespace
        ReadOnlyMemory<char>[] row = ["1".AsMemory(), "test".AsMemory(), "Alice".AsMemory()];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        TableSchema schema = result.Value;

        // Empty column names should be replaced with generated names
        schema.Columns[0].Name.Should().Be("Column1");
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("Column2");
        schema.Columns[1].Type.Should().Be(ColumnType.Text);
        schema.Columns[1].IsNullable.Should().BeFalse();

        schema.Columns[2].Name.Should().Be("name");
        schema.Columns[2].Type.Should().Be(ColumnType.Text);
        schema.Columns[2].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_NoColumns_ReturnsFailure()
    {
        // Arrange
        string[] columnNames = [];
        ReadOnlyMemory<char>[] row = [];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("CSV has no columns");
    }

    [Fact]
    public void ScanSchema_MismatchedColumnCount_ReturnsFailure()
    {
        // Arrange
        string[] columnNames = ["id", "name", "age"];
        ReadOnlyMemory<char>[] row =
        [
            "1".AsMemory(),
            "Alice".AsMemory(),
            // Missing third column
        ];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Row has 2 columns but header has 3 columns");
    }

    [Fact]
    public void ScanSchema_HeaderOnlyNoData_AllColumnsTextAndNullable()
    {
        // Arrange
        string[] columnNames = ["id", "name", "age"];
        ReadOnlyMemory<char>[] row =
        [
            "".AsMemory(), // Empty values
            "".AsMemory(),
            "".AsMemory(),
        ]; // No data rows

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        TableSchema schema = result.Value;

        // All columns should be Text and nullable when values are empty
        schema.Columns[0].Name.Should().Be("id");
        schema.Columns[0].Type.Should().Be(ColumnType.Text);
        schema.Columns[0].IsNullable.Should().BeTrue();

        schema.Columns[1].Name.Should().Be("name");
        schema.Columns[1].Type.Should().Be(ColumnType.Text);
        schema.Columns[1].IsNullable.Should().BeTrue();

        schema.Columns[2].Name.Should().Be("age");
        schema.Columns[2].Type.Should().Be(ColumnType.Text);
        schema.Columns[2].IsNullable.Should().BeTrue();
    }

    [Fact]
    public void ScanSchema_ComplexNumericValues_DetectsCorrectTypes()
    {
        // Arrange
        string[] columnNames = ["int", "negative_int", "decimal", "scientific", "zero"];
        ReadOnlyMemory<char>[] row =
        [
            "123456789".AsMemory(),
            "-987654321".AsMemory(),
            "3.14159265359".AsMemory(),
            "1.234E+10".AsMemory(),
            "0".AsMemory(),
        ];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        TableSchema schema = result.Value;

        schema.Columns[0].Name.Should().Be("int");
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("negative_int");
        schema.Columns[1].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[1].IsNullable.Should().BeFalse();

        schema.Columns[2].Name.Should().Be("decimal");
        schema.Columns[2].Type.Should().Be(ColumnType.FloatingPoint);
        schema.Columns[2].IsNullable.Should().BeFalse();

        schema.Columns[3].Name.Should().Be("scientific");
        schema.Columns[3].Type.Should().Be(ColumnType.FloatingPoint);
        schema.Columns[3].IsNullable.Should().BeFalse();

        schema.Columns[4].Name.Should().Be("zero");
        schema.Columns[4].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[4].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_DateFormats_DetectsTimestamp()
    {
        // Arrange
        string[] columnNames = ["iso_date", "iso_datetime", "common_date", "datetime_with_tz"];
        ReadOnlyMemory<char>[] row =
        [
            "2024-12-31".AsMemory(),
            "2024-12-31T23:59:59".AsMemory(),
            "12/31/2024".AsMemory(),
            "2024-12-31T23:59:59Z".AsMemory(),
        ];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        TableSchema schema = result.Value;

        schema.Columns[0].Name.Should().Be("iso_date");
        schema.Columns[0].Type.Should().Be(ColumnType.Timestamp);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("iso_datetime");
        schema.Columns[1].Type.Should().Be(ColumnType.Timestamp);
        schema.Columns[1].IsNullable.Should().BeFalse();

        schema.Columns[2].Name.Should().Be("common_date");
        schema.Columns[2].Type.Should().Be(ColumnType.Timestamp);
        schema.Columns[2].IsNullable.Should().BeFalse();

        schema.Columns[3].Name.Should().Be("datetime_with_tz");
        schema.Columns[3].Type.Should().Be(ColumnType.Timestamp);
        schema.Columns[3].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_BooleanVariations_DetectsBoolean()
    {
        // Arrange
        string[] columnNames = ["lower", "upper", "mixed", "with_spaces"];
        ReadOnlyMemory<char>[] row =
        [
            "true".AsMemory(),
            "FALSE".AsMemory(),
            "True".AsMemory(),
            "  false  ".AsMemory(),
        ];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        TableSchema schema = result.Value;

        schema.Columns[0].Name.Should().Be("lower");
        schema.Columns[0].Type.Should().Be(ColumnType.Boolean);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("upper");
        schema.Columns[1].Type.Should().Be(ColumnType.Boolean);
        schema.Columns[1].IsNullable.Should().BeFalse();

        schema.Columns[2].Name.Should().Be("mixed");
        schema.Columns[2].Type.Should().Be(ColumnType.Boolean);
        schema.Columns[2].IsNullable.Should().BeFalse();

        schema.Columns[3].Name.Should().Be("with_spaces");
        schema.Columns[3].Type.Should().Be(ColumnType.Boolean);
        schema.Columns[3].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_TextFallback_ForNonParsableValues()
    {
        // Arrange
        string[] columnNames = ["text", "mixed_alphanum", "invalid_date", "special_chars"];
        ReadOnlyMemory<char>[] row =
        [
            "hello world".AsMemory(),
            "abc123def".AsMemory(),
            "2024-13-45".AsMemory(), // Invalid date
            "@#$%^&*()".AsMemory(),
        ];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        TableSchema schema = result.Value;

        schema.Columns[0].Name.Should().Be("text");
        schema.Columns[0].Type.Should().Be(ColumnType.Text);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("mixed_alphanum");
        schema.Columns[1].Type.Should().Be(ColumnType.Text);
        schema.Columns[1].IsNullable.Should().BeFalse();

        schema.Columns[2].Name.Should().Be("invalid_date");
        schema.Columns[2].Type.Should().Be(ColumnType.Text);
        schema.Columns[2].IsNullable.Should().BeFalse();

        schema.Columns[3].Name.Should().Be("special_chars");
        schema.Columns[3].Type.Should().Be(ColumnType.Text);
        schema.Columns[3].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_TypePriority_BooleanOverNumber()
    {
        // "1" could be interpreted as WholeNumber, but not as Boolean
        // Test ensures Boolean detection only for "true"/"false"
        string[] columnNames = ["numeric_string"];
        ReadOnlyMemory<char>[] row = ["1".AsMemory()];

        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, [row]);

        result.IsSuccess.Should().BeTrue();
        TableSchema schema = result.Value;

        // "1" should be WholeNumber, not Boolean
        schema.Columns[0].Name.Should().Be("numeric_string");
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_TypePriority_WholeNumberOverFloatingPoint()
    {
        // "123" should be WholeNumber, not FloatingPoint
        string[] columnNames = ["integer"];
        ReadOnlyMemory<char>[] row = ["123".AsMemory()];

        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, [row]);

        result.IsSuccess.Should().BeTrue();
        TableSchema schema = result.Value;

        schema.Columns[0].Name.Should().Be("integer");
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_WithEmptyRowCollection_ReturnsTextNullableSchemaFromColumnNamesOnly()
    {
        // Arrange — no rows at all; schema inferred from column names only
        string[] columnNames = ["id", "name", "score"];
        IReadOnlyList<ReadOnlyMemory<char>[]> rows = [];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, rows);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ColumnCount.Should().Be(3);
        result.Value.Columns[0].Name.Should().Be("id");
        result.Value.Columns[0].Type.Should().Be(ColumnType.Text);
        result.Value.Columns[0].IsNullable.Should().BeTrue();
        result.Value.Columns[1].Name.Should().Be("name");
        result.Value.Columns[1].IsNullable.Should().BeTrue();
        result.Value.Columns[2].Name.Should().Be("score");
        result.Value.Columns[2].IsNullable.Should().BeTrue();
        result.Value.SourceFormat.Should().Be(DataFormat.Csv);
    }

    [Fact]
    public void ScanSchema_WithEmptyRowCollection_AndBlankColumnNames_GeneratesColumnNames()
    {
        // Arrange — blank column names in the header-only path should be auto-generated
        string[] columnNames = ["", "  ", "real_name"];
        IReadOnlyList<ReadOnlyMemory<char>[]> rows = [];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, rows);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns[0].Name.Should().Be("Column1");
        result.Value.Columns[1].Name.Should().Be("Column2");
        result.Value.Columns[2].Name.Should().Be("real_name");
    }

    [Fact]
    public void ScanSchema_WithZeroInitialScanCount_ProcessesFirstRowOnly()
    {
        // Arrange — second row conflicts the type to Text; with scanCount=0 the loop does not run
        string[] columnNames = ["value"];
        ReadOnlyMemory<char>[] row1 = ["42".AsMemory()];
        ReadOnlyMemory<char>[] row2 = ["not-a-number".AsMemory()];
        IReadOnlyList<ReadOnlyMemory<char>[]> rows = [row1, row2];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, rows, initialScanCount: 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void ScanSchema_WithNegativeInitialScanCount_ThrowsException()
    {
        // Arrange
        string[] columnNames = ["id"];
        ReadOnlyMemory<char>[] row = ["1".AsMemory()];

        // Act & Assert
        Action action = () => SchemaScanner.ScanSchema(columnNames, [row], initialScanCount: -1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ScanSchema_NullParameters_ThrowsException()
    {
        // Arrange
        string[] columnNames = ["id"];
        ReadOnlyMemory<char>[] row = ["1".AsMemory()];

        // Act & Assert
        Action action1 = () => SchemaScanner.ScanSchema(null!, [row]);
        action1.Should().Throw<ArgumentNullException>();

        Action action2 = () => SchemaScanner.ScanSchema(columnNames, null!);
        action2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ScanSchema_WithInitialScanCountLimitedTo200_ReturnsCorrectSchema()
    {
        // Arrange
        string[] columnNames = ["id", "value"];
        List<ReadOnlyMemory<char>[]> rows = [.. Enumerable
            .Range(0, 300)
            .Select(i =>
                new[]
                {
                    i.ToString(CultureInfo.InvariantCulture).AsMemory(),
                    // Even rows: floating point values, odd rows: whole numbers
                    (
                        i % 2 == 0
                            ? (i * 10 + 0.5).ToString(CultureInfo.InvariantCulture)
                            : (i * 10).ToString(CultureInfo.InvariantCulture)
                    ).AsMemory(),
                }
            )];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, rows, initialScanCount: 200);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ColumnCount.Should().Be(2);
        result.Value.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        // Second column should be FloatingPoint because it contains decimal values
        result.Value.Columns[1].Type.Should().Be(ColumnType.FloatingPoint);
    }

    [Fact]
    public void ScanSchema_WithLessThan200Rows_ScansAllRows()
    {
        // Arrange
        string[] columnNames = ["id"];
        List<ReadOnlyMemory<char>[]> rows = [.. Enumerable
            .Range(0, 100)
            .Select(i => new[] { i.ToString(CultureInfo.InvariantCulture).AsMemory() })];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, rows, initialScanCount: 200);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ColumnCount.Should().Be(1);
        result.Value.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void ScanSchema_WithCustomInitialScanCount_RespectsLimit()
    {
        // Arrange
        string[] columnNames = ["id"];
        List<ReadOnlyMemory<char>[]> rows = [.. Enumerable
            .Range(0, 150)
            .Select(i => new[] { i.ToString(CultureInfo.InvariantCulture).AsMemory() })];

        // Act - limit with custom initial scan count of 50
        Result<TableSchema> result = SchemaScanner.ScanSchema(columnNames, rows, initialScanCount: 50);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ColumnCount.Should().Be(1);
        result.Value.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
    }
}

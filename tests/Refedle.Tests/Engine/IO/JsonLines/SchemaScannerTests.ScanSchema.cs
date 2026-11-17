using AwesomeAssertions;
using Refedle.Engine;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed partial class SchemaScannerTests
{
    [Theory]
    [InlineData("1", ColumnType.WholeNumber, false)]
    [InlineData("1.5", ColumnType.FloatingPoint, false)]
    [InlineData("\"text\"", ColumnType.Text, false)]
    [InlineData("true", ColumnType.Boolean, false)]
    [InlineData("false", ColumnType.Boolean, false)]
    [InlineData("null", ColumnType.Text, true)] // null: type defaults to Text, always nullable
    public void ScanSchema_SingleValue_InfersCorrectType(
        string value,
        ColumnType expectedType,
        bool expectedNullable
    )
    {
        // Arrange
        string json = $"{{\"a\": {value}}}";
        JsonRawBytes[] lines = [Line(json)];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", expectedType, expectedNullable);
    }

    [Theory]
    [InlineData("1", "1.5", ColumnType.FloatingPoint)]
    [InlineData("1", "\"text\"", ColumnType.Text)]
    [InlineData("true", "false", ColumnType.Boolean)]
    [InlineData("1", "true", ColumnType.Text)]
    public void ScanSchema_TypeConflict_ResolvesExpectedColumnType(
        string val1,
        string val2,
        ColumnType expectedType
    )
    {
        // Arrange
        string json1 = $"{{\"a\": {val1}}}";
        string json2 = $"{{\"a\": {val2}}}";
        JsonRawBytes[] lines = [Line(json1), Line(json2)];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", expectedType, false);
    }

    [Fact]
    public void ScanSchema_MultiLineConsistentTypes_PreservesTypes()
    {
        // Arrange
        JsonRawBytes[] lines =
        [
            Line("{\"a\": 1, \"b\": \"text\"}"),
            Line("{\"a\": 2, \"b\": \"text2\"}"),
        ];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", ColumnType.WholeNumber, false);
        AssertColumn(result.Value, 1, "b", ColumnType.Text, false);
    }

    [Fact]
    public void ScanSchema_MissingKeyInSomeRows_MarksNullable()
    {
        // Arrange
        JsonRawBytes[] lines =
        [
            Line("{\"a\": 1}"),
            Line("{\"a\": 2}"),
            Line("{}"), // 'a' is missing
        ];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", ColumnType.WholeNumber, true);
    }

    [Fact]
    public void ScanSchema_NewKeyInLaterRow_AddsNullableColumn()
    {
        // Arrange
        JsonRawBytes[] lines = [Line("{\"a\": 1}"), Line("{\"a\": 2, \"b\": \"text\"}")];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", ColumnType.WholeNumber, false);
        AssertColumn(result.Value, 1, "b", ColumnType.Text, true);
    }

    [Fact]
    public void ScanSchema_NullValueInRow_TypeUnchangedAndNullable()
    {
        // Arrange
        JsonRawBytes[] lines = [Line("{\"a\": 1}"), Line("{\"a\": null}")];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        AssertColumn(result.Value, 0, "a", ColumnType.WholeNumber, true);
    }

    [Fact]
    public void ScanSchema_EmptyInput_ReturnsFailure()
    {
        // Arrange
        JsonRawBytes[] lines = [];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ScanSchema_AllLinesMalformed_ReturnsFailure()
    {
        // Arrange
        JsonRawBytes[] lines =
        [
            new JsonRawBytes("not json"u8.ToArray()),
            new JsonRawBytes("also not json"u8.ToArray()),
        ];

        // Act
        Result<TableSchema> result = SchemaScanner.ScanSchema(lines);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ScanSchema_NegativeInitialScanCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        JsonRawBytes[] lines = [Line("{\"a\": 1}")];

        // Act
        Action act = () => SchemaScanner.ScanSchema(lines, -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

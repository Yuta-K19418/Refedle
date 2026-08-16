using AwesomeAssertions;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.Models;

public sealed class SchemaTests
{
    [Fact]
    public void GetColumn_WithExistingName_ReturnsColumn()
    {
        // Arrange
        var schema = CreateTableSchema();

        // Act
        var column = schema.GetColumn("name");

        // Assert
        column.Should().NotBeNull();
        column.Name.Should().Be("name");
        column.Type.Should().Be(ColumnType.Text);
    }

    [Fact]
    public void GetColumn_WithMissingName_ReturnsNull()
    {
        // Arrange
        var schema = CreateTableSchema();

        // Act
        var column = schema.GetColumn("missing");

        // Assert
        column.Should().BeNull();
    }

    [Fact]
    public void ContainsColumn_WithExistingAndMissingNames_ReturnsExpectedValues()
    {
        // Arrange
        var schema = CreateTableSchema();

        // Act
        var containsId = schema.ContainsColumn("id");
        var containsMissing = schema.ContainsColumn("missing");

        // Assert
        containsId.Should().BeTrue();
        containsMissing.Should().BeFalse();
    }

    [Fact]
    public void ColumnCount_WithThreeColumns_ReturnsThree()
    {
        // Arrange
        var schema = CreateTableSchema();

        // Act
        var count = schema.ColumnCount;

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void Constructor_WithDuplicateColumnNames_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new TableSchema
        {
            Columns =
            [
                new() { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 },
                new() { Name = "id", Type = ColumnType.Text, ColumnIndex = 1 },
            ],
            SourceFormat = DataFormat.JsonArray,
        };

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void Constructor_WithMultipleDuplicateColumnNames_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new TableSchema
        {
            Columns =
            [
                new() { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 },
                new() { Name = "name", Type = ColumnType.Text, ColumnIndex = 1 },
                new() { Name = "id", Type = ColumnType.Text, ColumnIndex = 2 },
                new() { Name = "name", Type = ColumnType.Text, ColumnIndex = 3 },
            ],
            SourceFormat = DataFormat.JsonArray,
        };

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ColumnSchema_WithoutValidName_ThrowsArgumentException(string? name)
    {
        // Arrange & Act
        var act = () => new ColumnSchema { Name = name!, Type = ColumnType.Text, ColumnIndex = 0 };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ColumnSchema_WithNegativeColumnIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act
        var act = () => new ColumnSchema { Name = "test", Type = ColumnType.Text, ColumnIndex = -1 };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TableSchema_WithNoColumns_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new TableSchema { Columns = [], SourceFormat = DataFormat.JsonArray };

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void Constructor_WithValidColumns_DoesNotThrow()
    {
        // Arrange & Act
        var act = () => new TableSchema
        {
            Columns = [new() { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 }],
            SourceFormat = DataFormat.JsonArray,
        };

        // Assert
        act.Should().NotThrow();
    }

    private static TableSchema CreateTableSchema()
    {
        return new TableSchema
        {
            Columns =
            [
                new() { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 },
                new() { Name = "name", Type = ColumnType.Text, ColumnIndex = 1 },
                new() { Name = "price", Type = ColumnType.FloatingPoint, ColumnIndex = 2 },
            ],
            SourceFormat = DataFormat.JsonArray,
        };
    }
}

using AwesomeAssertions;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.Types;

/// <summary>
/// Tests for <see cref="DataFormatExtensions"/>.
/// </summary>
public sealed class DataFormatExtensionsTests
{
    [Theory]
    [InlineData(DataFormat.Csv, "CSV")]
    [InlineData(DataFormat.JsonLines, "JSON Lines")]
    [InlineData(DataFormat.JsonArray, "JSON Array")]
    [InlineData(DataFormat.JsonObject, "JSON Object")]
    public void GetDisplayName_WithValidFormat_ReturnsExpectedName(
        DataFormat format,
        string expected
    )
    {
        // Arrange
        // (parameters provided via [InlineData])

        // Act
        string result = format.GetDisplayName();

        // Assert
        result.Should().Be(expected);
    }

    public static TheoryData<DataFormat, IReadOnlySet<ColumnType>> GetValidCastTargetsTestData =>
        new()
        {
            { DataFormat.Csv, _csvSet },
            { DataFormat.JsonLines, _jsonSet },
            { DataFormat.JsonArray, _jsonSet },
            { DataFormat.JsonObject, _jsonSet },
        };

    private static readonly IReadOnlySet<ColumnType> _csvSet = new HashSet<ColumnType>
    {
        ColumnType.Text,
        ColumnType.WholeNumber,
        ColumnType.FloatingPoint,
        ColumnType.Boolean,
        ColumnType.Timestamp,
    };

    private static readonly IReadOnlySet<ColumnType> _jsonSet = new HashSet<ColumnType>
    {
        ColumnType.Text,
        ColumnType.WholeNumber,
        ColumnType.FloatingPoint,
        ColumnType.Boolean,
        ColumnType.Timestamp,
        ColumnType.JsonObject,
        ColumnType.JsonArray,
    };

    [Theory]
    [MemberData(nameof(GetValidCastTargetsTestData))]
    public void GetValidCastTargets_WithValidFormat_ReturnsExpectedTypes(
        DataFormat format,
        IReadOnlySet<ColumnType> expectedTypes
    )
    {
        // Arrange
        // (parameters provided via MemberData)

        // Act
        IReadOnlySet<ColumnType> result = format.GetValidCastTargets();

        // Assert
        result.Should().BeEquivalentTo(expectedTypes);
    }

    [Fact]
    public void GetValidCastTargets_WithInvalidFormat_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        DataFormat invalidFormat = (DataFormat)999;

        // Act
        Exception exception = Record.Exception(() => invalidFormat.GetValidCastTargets());

        // Assert
        exception.Should().BeOfType<ArgumentOutOfRangeException>();
    }
}

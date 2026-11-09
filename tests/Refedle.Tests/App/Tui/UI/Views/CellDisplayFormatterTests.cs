using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed class CellDisplayFormatterTests
{
    [Theory]
    [InlineData("007", ColumnType.Text)]
    [InlineData("{\"a\":1}", ColumnType.JsonObject)]
    [InlineData("[1,2]", ColumnType.JsonArray)]
    public void FormatCellValue_WithNonNumericTypes_ReturnsRawValue(string rawValue, ColumnType type)
    {
        // Arrange — the raw value and column type are the complete input

        // Act
        var result = CellDisplayFormatter.FormatCellValue(rawValue, type, formatString: null);

        // Assert
        result.Should().Be(rawValue);
    }

    [Theory]
    [InlineData("TRUE", "true")]
    [InlineData("FALSE", "false")]
    public void FormatCellValue_WithBooleanCast_ReturnsCanonicalLowercase(string rawValue, string expected)
    {
        // Arrange — the raw value and column type are the complete input

        // Act
        var result = CellDisplayFormatter.FormatCellValue(rawValue, ColumnType.Boolean, formatString: null);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "2024-03-20 14:05:33")]
    [InlineData("", "2024-03-20 14:05:33")]
    [InlineData("yyyy/MM/dd", "2024/03/20")]
    public void FormatCellValue_WithTimestampCast_AppliesSelectedFormat(string? formatString, string expected)
    {
        // Arrange
        const string RawTimestamp = "2024-03-20T14:05:33";

        // Act
        var result = CellDisplayFormatter.FormatCellValue(RawTimestamp, ColumnType.Timestamp, formatString);

        // Assert
        result.Should().Be(expected);
    }
}

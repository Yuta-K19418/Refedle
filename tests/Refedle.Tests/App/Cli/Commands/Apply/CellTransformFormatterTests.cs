using AwesomeAssertions;
using Refedle.App.Cli.Commands.Apply;
using Refedle.App.Cli.IO;
using Refedle.Engine.Models;

namespace Refedle.Tests.App.Cli.Commands.Apply;

// CellPresence/CellEncoding are internal, so each Presence x Encoding combination is a standalone case.
public sealed class CellTransformFormatterTests
{
    // -------------------------------------------------------------------------
    // Format — FillSpec
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_WithFillSpec_TextValue_ReturnsPlainTextValueCell()
    {
        // Arrange
        var cell = new CellData("original", CellPresence.Value);
        var transform = new FillSpec("ANON");

        // Act
        var result = CellTransformFormatter.Format(transform, cell);

        // Assert
        result.Value.ToString().Should().Be("ANON");
        result.Presence.Should().Be(CellPresence.Value);
        result.Encoding.Should().Be(CellEncoding.PlainText);
    }

    [Fact]
    public void Format_WithFillSpec_NumericValue_ReturnsNumericValueCell()
    {
        // Arrange
        var cell = new CellData("original", CellPresence.Value);
        var transform = new FillSpec("123");

        // Act
        var result = CellTransformFormatter.Format(transform, cell);

        // Assert
        result.Value.ToString().Should().Be("123");
        result.Presence.Should().Be(CellPresence.Value);
        result.Encoding.Should().Be(CellEncoding.Numeric);
    }

    [Fact]
    public void Format_WithFillSpec_BooleanValue_ReturnsBooleanValueCell()
    {
        // Arrange
        var cell = new CellData("original", CellPresence.Value);
        var transform = new FillSpec("true");

        // Act
        var result = CellTransformFormatter.Format(transform, cell);

        // Assert
        result.Value.ToString().Should().Be("true");
        result.Presence.Should().Be(CellPresence.Value);
        result.Encoding.Should().Be(CellEncoding.Boolean);
    }

    [Fact]
    public void Format_WithFillSpec_NonValueCell_OverwritesWithValueCell()
    {
        // Arrange
        var cell = new CellData([], CellPresence.Null);
        var transform = new FillSpec("N/A");

        // Act
        var result = CellTransformFormatter.Format(transform, cell);

        // Assert
        result.Value.ToString().Should().Be("N/A");
        result.Presence.Should().Be(CellPresence.Value);
    }

    // -------------------------------------------------------------------------
    // Format — TimestampFormatSpec: parseable values
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("2024-03-15T10:30:00", "yyyy/MM/dd", "2024/03/15")]
    [InlineData("03/15/2024", "yyyy-MM-dd", "2024-03-15")]
    public void Format_WithTimestampFormatSpec_ParseableValue_ReturnsReformattedValueCell(
        string raw,
        string targetFormat,
        string expected)
    {
        // Arrange
        var cell = new CellData(raw, CellPresence.Value);
        var transform = new TimestampFormatSpec(targetFormat);

        // Act
        var result = CellTransformFormatter.Format(transform, cell);

        // Assert
        result.Value.ToString().Should().Be(expected);
        result.Presence.Should().Be(CellPresence.Value);
    }

    // -------------------------------------------------------------------------
    // Format — TimestampFormatSpec: unparseable values pass through unchanged
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("")]
    [InlineData("   ")]
    public void Format_WithTimestampFormatSpec_UnparseableValue_ReturnsOriginalCell(string raw)
    {
        // Arrange
        var cell = new CellData(raw, CellPresence.Value);
        var transform = new TimestampFormatSpec("yyyy/MM/dd");

        // Act
        var result = CellTransformFormatter.Format(transform, cell);

        // Assert
        result.Value.ToString().Should().Be(raw);
        result.Presence.Should().Be(CellPresence.Value);
    }

    [Fact]
    public void Format_WithTimestampFormatSpec_UnparseableValue_PreservesOriginalEncoding()
    {
        // Raw cells (embedded JSON objects/arrays) must keep Raw encoding so the
        // JSON writer emits them verbatim instead of as a quoted string.
        // Arrange
        var cell = new CellData("{\"a\":1}", CellPresence.Value, CellEncoding.Raw);
        var transform = new TimestampFormatSpec("yyyy/MM/dd");

        // Act
        var result = CellTransformFormatter.Format(transform, cell);

        // Assert
        result.Value.ToString().Should().Be("{\"a\":1}");
        result.Presence.Should().Be(CellPresence.Value);
        result.Encoding.Should().Be(CellEncoding.Raw);
    }

    // -------------------------------------------------------------------------
    // Format — TimestampFormatSpec: non-value cells pass through unchanged
    // -------------------------------------------------------------------------

    [Fact]
    public void Format_WithTimestampFormatSpec_NullCell_ReturnsOriginalCell()
    {
        // Arrange
        var cell = new CellData([], CellPresence.Null);
        var transform = new TimestampFormatSpec("yyyy/MM/dd");

        // Act
        var result = CellTransformFormatter.Format(transform, cell);

        // Assert
        result.Presence.Should().Be(CellPresence.Null);
        result.Value.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Format_WithTimestampFormatSpec_MissingCell_ReturnsOriginalCell()
    {
        // Arrange
        var cell = new CellData([], CellPresence.Missing);
        var transform = new TimestampFormatSpec("yyyy/MM/dd");

        // Act
        var result = CellTransformFormatter.Format(transform, cell);

        // Assert
        result.Presence.Should().Be(CellPresence.Missing);
        result.Value.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Format_WithTimestampFormatSpec_InvalidCell_ReturnsOriginalCell()
    {
        // Arrange
        var cell = new CellData([], CellPresence.Invalid);
        var transform = new TimestampFormatSpec("yyyy/MM/dd");

        // Act
        var result = CellTransformFormatter.Format(transform, cell);

        // Assert
        result.Presence.Should().Be(CellPresence.Invalid);
        result.Value.IsEmpty.Should().BeTrue();
    }
}

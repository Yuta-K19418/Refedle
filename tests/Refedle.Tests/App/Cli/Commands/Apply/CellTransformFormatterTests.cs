using AwesomeAssertions;
using Refedle.App.Cli.Commands.Apply;
using Refedle.App.Cli.IO;
using Refedle.Engine.Models;

namespace Refedle.Tests.App.Cli.Commands.Apply;

// CellPresence/CellEncoding are internal, so each Presence x Encoding combination is a standalone case.
public sealed class CellTransformFormatterTests
{
    // -------------------------------------------------------------------------
    // TryFormat — FillSpec
    // -------------------------------------------------------------------------

    [Fact]
    public void TryFormat_WithFillSpec_TextValue_ReturnsPlainTextValueCell()
    {
        // Arrange
        CellData cell = new("original", CellPresence.Value);
        FillSpec transform = new("ANON");

        // Act
        bool success = CellTransformFormatter.TryFormat(transform, cell, out CellData result, out string? reason);

        // Assert
        success.Should().BeTrue();
        reason.Should().BeEmpty();
        result.Value.ToString().Should().Be("ANON");
        result.Presence.Should().Be(CellPresence.Value);
        result.Encoding.Should().Be(CellEncoding.PlainText);
    }

    [Fact]
    public void TryFormat_WithFillSpec_NumericValue_ReturnsNumericValueCell()
    {
        // Arrange
        CellData cell = new("original", CellPresence.Value);
        FillSpec transform = new("123");

        // Act
        bool success = CellTransformFormatter.TryFormat(transform, cell, out CellData result, out string? reason);

        // Assert
        success.Should().BeTrue();
        reason.Should().BeEmpty();
        result.Value.ToString().Should().Be("123");
        result.Presence.Should().Be(CellPresence.Value);
        result.Encoding.Should().Be(CellEncoding.Numeric);
    }

    [Fact]
    public void TryFormat_WithFillSpec_BooleanValue_ReturnsBooleanValueCell()
    {
        // Arrange
        CellData cell = new("original", CellPresence.Value);
        FillSpec transform = new("true");

        // Act
        bool success = CellTransformFormatter.TryFormat(transform, cell, out CellData result, out string? reason);

        // Assert
        success.Should().BeTrue();
        reason.Should().BeEmpty();
        result.Value.ToString().Should().Be("true");
        result.Presence.Should().Be(CellPresence.Value);
        result.Encoding.Should().Be(CellEncoding.Boolean);
    }

    [Fact]
    public void TryFormat_WithFillSpec_NonValueCell_OverwritesWithValueCell()
    {
        // Arrange
        CellData cell = new([], CellPresence.Null);
        FillSpec transform = new("N/A");

        // Act
        bool success = CellTransformFormatter.TryFormat(transform, cell, out CellData result, out string? reason);

        // Assert
        success.Should().BeTrue();
        reason.Should().BeEmpty();
        result.Value.ToString().Should().Be("N/A");
        result.Presence.Should().Be(CellPresence.Value);
    }

    // -------------------------------------------------------------------------
    // TryFormat — TimestampFormatSpec: parseable values
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("2024-03-15T10:30:00", "yyyy/MM/dd", "2024/03/15")]
    [InlineData("03/15/2024", "yyyy-MM-dd", "2024-03-15")]
    public void TryFormat_WithTimestampFormatSpec_ParseableValue_ReturnsReformattedValueCell(
        string raw,
        string targetFormat,
        string expected)
    {
        // Arrange
        CellData cell = new(raw, CellPresence.Value);
        TimestampFormatSpec transform = new(targetFormat);

        // Act
        bool success = CellTransformFormatter.TryFormat(transform, cell, out CellData result, out string? reason);

        // Assert
        success.Should().BeTrue();
        reason.Should().BeEmpty();
        result.Value.ToString().Should().Be(expected);
        result.Presence.Should().Be(CellPresence.Value);
    }

    // -------------------------------------------------------------------------
    // TryFormat — TimestampFormatSpec: unparseable values pass through with a reason
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryFormat_WithTimestampFormatSpec_UnparseableValue_ReturnsOriginalCellAndReason(string raw)
    {
        // Arrange
        CellData cell = new(raw, CellPresence.Value);
        TimestampFormatSpec transform = new("yyyy/MM/dd");

        // Act
        bool success = CellTransformFormatter.TryFormat(transform, cell, out CellData result, out string? reason);

        // Assert
        success.Should().BeFalse();
        reason.Should().NotBeEmpty();
        result.Value.ToString().Should().Be(raw);
        result.Presence.Should().Be(CellPresence.Value);
    }

    [Fact]
    public void TryFormat_WithTimestampFormatSpec_UnparseableValue_PreservesOriginalEncoding()
    {
        // Raw cells (embedded JSON objects/arrays) must keep Raw encoding so the
        // JSON writer emits them verbatim instead of as a quoted string.
        // Arrange
        CellData cell = new("{\"a\":1}", CellPresence.Value, CellEncoding.Raw);
        TimestampFormatSpec transform = new("yyyy/MM/dd");

        // Act
        bool success = CellTransformFormatter.TryFormat(transform, cell, out CellData result, out string? reason);

        // Assert
        success.Should().BeFalse();
        reason.Should().NotBeEmpty();
        result.Value.ToString().Should().Be("{\"a\":1}");
        result.Presence.Should().Be(CellPresence.Value);
        result.Encoding.Should().Be(CellEncoding.Raw);
    }

    // -------------------------------------------------------------------------
    // TryFormat — TimestampFormatSpec: non-value cells pass through without a reason
    // -------------------------------------------------------------------------

    [Fact]
    public void TryFormat_WithTimestampFormatSpec_NullCell_ReturnsOriginalCell()
    {
        // Arrange
        CellData cell = new([], CellPresence.Null);
        TimestampFormatSpec transform = new("yyyy/MM/dd");

        // Act
        bool success = CellTransformFormatter.TryFormat(transform, cell, out CellData result, out string? reason);

        // Assert
        success.Should().BeTrue();
        reason.Should().BeEmpty();
        result.Presence.Should().Be(CellPresence.Null);
        result.Value.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TryFormat_WithTimestampFormatSpec_MissingCell_ReturnsOriginalCell()
    {
        // Arrange
        CellData cell = new([], CellPresence.Missing);
        TimestampFormatSpec transform = new("yyyy/MM/dd");

        // Act
        bool success = CellTransformFormatter.TryFormat(transform, cell, out CellData result, out string? reason);

        // Assert
        success.Should().BeTrue();
        reason.Should().BeEmpty();
        result.Presence.Should().Be(CellPresence.Missing);
        result.Value.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TryFormat_WithTimestampFormatSpec_InvalidCell_ReturnsOriginalCell()
    {
        // Arrange
        CellData cell = new([], CellPresence.Invalid);
        TimestampFormatSpec transform = new("yyyy/MM/dd");

        // Act
        bool success = CellTransformFormatter.TryFormat(transform, cell, out CellData result, out string? reason);

        // Assert
        success.Should().BeTrue();
        reason.Should().BeEmpty();
        result.Presence.Should().Be(CellPresence.Invalid);
        result.Value.IsEmpty.Should().BeTrue();
    }
}

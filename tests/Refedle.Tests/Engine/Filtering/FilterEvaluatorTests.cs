using AwesomeAssertions;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.Filtering;

public sealed class FilterEvaluatorTests
{
    // -------------------------------------------------------------------------
    // Text operators — Equals / NotEquals
    // -------------------------------------------------------------------------

    [Fact]
    public void EvaluateFilter_Equals_MatchingValue_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Equals,
            Value: "Alice"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("Alice".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_Equals_NonMatchingValue_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Equals,
            Value: "Alice"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("Bob".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_Equals_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Equals,
            Value: "alice"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("ALICE".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_NotEquals_MatchingValue_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.NotEquals,
            Value: "Alice"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("Alice".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_NotEquals_NonMatchingValue_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.NotEquals,
            Value: "Alice"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("Bob".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Text operators — Contains / NotContains
    // -------------------------------------------------------------------------

    [Fact]
    public void EvaluateFilter_Contains_ValuePresent_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Contains,
            Value: "app"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_Contains_ValueAbsent_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Contains,
            Value: "xyz"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_Contains_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.Contains,
            Value: "app"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("APPLE".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_NotContains_ValuePresent_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.NotContains,
            Value: "app"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_NotContains_ValueAbsent_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.NotContains,
            Value: "xyz"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Text operators — StartsWith / EndsWith
    // -------------------------------------------------------------------------

    [Fact]
    public void EvaluateFilter_StartsWith_MatchingPrefix_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.StartsWith,
            Value: "app"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_StartsWith_NonMatchingPrefix_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.StartsWith,
            Value: "xyz"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_EndsWith_MatchingSuffix_ReturnsTrue()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.EndsWith,
            Value: "ple"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_EndsWith_NonMatchingSuffix_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.EndsWith,
            Value: "xyz"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("apple".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Numeric operators — WholeNumber (boundary: threshold = 20)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("19", false)]   // below threshold
    [InlineData("20", false)]   // at threshold
    [InlineData("21", true)]    // above threshold
    public void EvaluateFilter_GreaterThan_WholeNumber_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("19", false)]   // below threshold
    [InlineData("20", true)]    // at threshold
    [InlineData("21", true)]    // above threshold
    public void EvaluateFilter_GreaterThanOrEqual_WholeNumber_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThanOrEqual,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("19", true)]    // below threshold
    [InlineData("20", false)]   // at threshold
    [InlineData("21", false)]   // above threshold
    public void EvaluateFilter_LessThan_WholeNumber_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.LessThan,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("19", true)]    // below threshold
    [InlineData("20", true)]    // at threshold
    [InlineData("21", false)]   // above threshold
    public void EvaluateFilter_LessThanOrEqual_WholeNumber_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.LessThanOrEqual,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_WholeNumber_InvalidRawValue_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("abc".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_WholeNumber_WhitespaceValue_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "20"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(" 15 ".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_WholeNumber_NegativeValues_WorksCorrectly()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "-10"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("-5".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_WholeNumber_ZeroBoundary_WorksCorrectly()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.WholeNumber,
            Operator: FilterOperator.GreaterThan,
            Value: "0"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("0".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Numeric operators — FloatingPoint (boundary: threshold = 2.0)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("1.9", false)]   // below threshold
    [InlineData("2.0", false)]   // at threshold
    [InlineData("2.1", true)]    // above threshold
    public void EvaluateFilter_GreaterThan_FloatingPoint_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.FloatingPoint,
            Operator: FilterOperator.GreaterThan,
            Value: "2.0"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.9", false)]   // below threshold
    [InlineData("2.0", true)]    // at threshold
    [InlineData("2.1", true)]    // above threshold
    public void EvaluateFilter_GreaterThanOrEqual_FloatingPoint_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.FloatingPoint,
            Operator: FilterOperator.GreaterThanOrEqual,
            Value: "2.0"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.9", true)]    // below threshold
    [InlineData("2.0", false)]   // at threshold
    [InlineData("2.1", false)]   // above threshold
    public void EvaluateFilter_LessThan_FloatingPoint_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.FloatingPoint,
            Operator: FilterOperator.LessThan,
            Value: "2.0"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.9", true)]    // below threshold
    [InlineData("2.0", true)]    // at threshold
    [InlineData("2.1", false)]   // above threshold
    public void EvaluateFilter_LessThanOrEqual_FloatingPoint_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.FloatingPoint,
            Operator: FilterOperator.LessThanOrEqual,
            Value: "2.0"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_FloatingPoint_NegativeValues_WorksCorrectly()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.FloatingPoint,
            Operator: FilterOperator.GreaterThan,
            Value: "-10.5"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("-5.3".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_FloatingPoint_ScientificNotation_WorksCorrectly()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.FloatingPoint,
            Operator: FilterOperator.GreaterThan,
            Value: "1.5"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("2.5e2".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Timestamp operators (boundary: threshold = 2024-06-15)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("2024-06-14", false)]   // day before threshold
    [InlineData("2024-06-15", false)]   // at threshold
    [InlineData("2024-06-16", true)]    // day after threshold
    public void EvaluateFilter_GreaterThan_Timestamp_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Timestamp,
            Operator: FilterOperator.GreaterThan,
            Value: "2024-06-15"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("2024-06-14", false)]   // day before threshold
    [InlineData("2024-06-15", true)]    // at threshold
    [InlineData("2024-06-16", true)]    // day after threshold
    public void EvaluateFilter_GreaterThanOrEqual_Timestamp_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Timestamp,
            Operator: FilterOperator.GreaterThanOrEqual,
            Value: "2024-06-15"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("2024-06-14", true)]    // day before threshold
    [InlineData("2024-06-15", false)]   // at threshold
    [InlineData("2024-06-16", false)]   // day after threshold
    public void EvaluateFilter_LessThan_Timestamp_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Timestamp,
            Operator: FilterOperator.LessThan,
            Value: "2024-06-15"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("2024-06-14", true)]    // day before threshold
    [InlineData("2024-06-15", true)]    // at threshold
    [InlineData("2024-06-16", false)]   // day after threshold
    public void EvaluateFilter_LessThanOrEqual_Timestamp_BoundaryValues_ReturnsExpected(
        string rawValue, bool expected)
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Timestamp,
            Operator: FilterOperator.LessThanOrEqual,
            Value: "2024-06-15"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void EvaluateFilter_GreaterThan_Timestamp_InvalidRawValue_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Timestamp,
            Operator: FilterOperator.GreaterThan,
            Value: "2024-01-01"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("not-a-date".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Text column with numeric operators — fallback to false
    // -------------------------------------------------------------------------

    [Fact]
    public void EvaluateFilter_GreaterThan_TextColumn_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.GreaterThan,
            Value: "hello"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("world".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFilter_LessThan_TextColumn_ReturnsFalse()
    {
        // Arrange
        var spec = new FilterSpec(
            SourceColumnIndex: 0,
            ColumnType: ColumnType.Text,
            Operator: FilterOperator.LessThan,
            Value: "hello"
        );

        // Act
        var result = FilterEvaluator.EvaluateFilter("world".AsSpan(), spec);

        // Assert
        result.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // BatchFilterSpec — CLI per-row type resolution (ComparisonType-based)
    // -------------------------------------------------------------------------

    [Fact]
    public void EvaluateFilter_BatchText_Equals_MatchingValue_ReturnsTrue()
    {
        // Arrange
        var spec = new BatchFilterSpec(
            SourceColumnIndex: 0,
            ComparisonType: ComparisonType.Text,
            Operator: FilterOperator.Equals,
            Value: "hello");

        // Act
        var result = FilterEvaluator.EvaluateFilter("hello".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("42", true)]    // whole number: 42 > 40
    [InlineData("40.5", true)]  // floating point: 40.5 > 40 (would be excluded if forced to whole)
    [InlineData("30", false)]   // whole number: 30 > 40 is false
    public void EvaluateFilter_BatchNumber_ResolvesTypePerRow(string rawValue, bool expected)
    {
        // Arrange
        var spec = new BatchFilterSpec(
            SourceColumnIndex: 0,
            ComparisonType: ComparisonType.Number,
            Operator: FilterOperator.GreaterThan,
            Value: "40");

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void EvaluateFilter_BatchNumber_WholeValueWithFractionalThreshold_UsesNumericComparison()
    {
        // Arrange — regression: an integer row value compared against a fractional threshold
        // must still compare (as double), not fail by forcing the threshold into whole-number parsing.
        var spec = new BatchFilterSpec(
            SourceColumnIndex: 0,
            ComparisonType: ComparisonType.Number,
            Operator: FilterOperator.GreaterThan,
            Value: "40.5");

        // Act
        var result = FilterEvaluator.EvaluateFilter("42".AsSpan(), spec);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("2024-06-01", true)]
    [InlineData("2023-01-01", false)]
    public void EvaluateFilter_BatchTimestamp_OrdersChronologically(string rawValue, bool expected)
    {
        // Arrange
        var spec = new BatchFilterSpec(
            SourceColumnIndex: 0,
            ComparisonType: ComparisonType.Timestamp,
            Operator: FilterOperator.GreaterThan,
            Value: "2024-01-01");

        // Act
        var result = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);

        // Assert
        result.Should().Be(expected);
    }
}

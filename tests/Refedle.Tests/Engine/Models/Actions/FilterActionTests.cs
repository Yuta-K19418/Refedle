using AwesomeAssertions;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.Engine.Models.Actions;

public sealed class FilterActionTests
{
    // -------------------------------------------------------------------------
    // Description property
    // -------------------------------------------------------------------------

    [Fact]
    public void Description_ReturnsExpectedFormat()
    {
        // Arrange
        var action = FilterAction.Create("Price", FilterOperator.GreaterThan, ComparisonType.Number, "100").Value;

        // Act
        var description = action.Description;

        // Assert
        description.Should().Be("Filter 'Price' GreaterThan '100'");
    }

    // -------------------------------------------------------------------------
    // Validate — valid operator / ComparisonType combination with a parseable value
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(FilterOperator.Equals, ComparisonType.Text, "anything")]
    [InlineData(FilterOperator.NotEquals, ComparisonType.Text, "")]
    [InlineData(FilterOperator.Contains, ComparisonType.Text, "sub")]
    [InlineData(FilterOperator.NotContains, ComparisonType.Text, "sub")]
    [InlineData(FilterOperator.StartsWith, ComparisonType.Text, "pre")]
    [InlineData(FilterOperator.EndsWith, ComparisonType.Text, "suf")]
    [InlineData(FilterOperator.Equals, ComparisonType.Number, "42")]
    [InlineData(FilterOperator.NotEquals, ComparisonType.Number, "-1.5")]
    [InlineData(FilterOperator.GreaterThan, ComparisonType.Number, "100")]
    [InlineData(FilterOperator.LessThan, ComparisonType.Number, "0")]
    [InlineData(FilterOperator.GreaterThanOrEqual, ComparisonType.Number, "3.14")]
    [InlineData(FilterOperator.LessThanOrEqual, ComparisonType.Number, "1e3")]
    [InlineData(FilterOperator.Equals, ComparisonType.Timestamp, "2025-01-01T00:00:00Z")]
    [InlineData(FilterOperator.NotEquals, ComparisonType.Timestamp, "2025-01-01")]
    [InlineData(FilterOperator.GreaterThan, ComparisonType.Timestamp, "2025-01-01")]
    [InlineData(FilterOperator.LessThan, ComparisonType.Timestamp, "2025-01-01")]
    [InlineData(FilterOperator.GreaterThanOrEqual, ComparisonType.Timestamp, "2025-01-01")]
    [InlineData(FilterOperator.LessThanOrEqual, ComparisonType.Timestamp, "2025-01-01")]
    public void Validate_ValidCombinationAndValue_ReturnsSuccess(
        FilterOperator op, ComparisonType comparisonType, string value)
    {
        // Act
        var result = FilterAction.Validate(op, comparisonType, value);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Validate — invalid operator / ComparisonType combination
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(FilterOperator.Contains, ComparisonType.Number)]
    [InlineData(FilterOperator.NotContains, ComparisonType.Number)]
    [InlineData(FilterOperator.StartsWith, ComparisonType.Number)]
    [InlineData(FilterOperator.EndsWith, ComparisonType.Number)]
    [InlineData(FilterOperator.Contains, ComparisonType.Timestamp)]
    [InlineData(FilterOperator.NotContains, ComparisonType.Timestamp)]
    [InlineData(FilterOperator.StartsWith, ComparisonType.Timestamp)]
    [InlineData(FilterOperator.EndsWith, ComparisonType.Timestamp)]
    [InlineData(FilterOperator.GreaterThan, ComparisonType.Text)]
    [InlineData(FilterOperator.LessThan, ComparisonType.Text)]
    [InlineData(FilterOperator.GreaterThanOrEqual, ComparisonType.Text)]
    [InlineData(FilterOperator.LessThanOrEqual, ComparisonType.Text)]
    public void Validate_InvalidOperatorCombination_ReturnsFailure(
        FilterOperator op, ComparisonType comparisonType)
    {
        // Act
        var result = FilterAction.Validate(op, comparisonType, "ignored");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not valid for comparison type");
    }

    // -------------------------------------------------------------------------
    // Validate — value not parseable as the declared ComparisonType
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void Validate_NumberWithUnparseableOrNonFiniteValue_ReturnsFailure(string value)
    {
        // Act
        var result = FilterAction.Validate(FilterOperator.GreaterThan, ComparisonType.Number, value);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not parseable");
    }

    [Fact]
    public void Validate_TimestampWithUnparseableValue_ReturnsFailure()
    {
        // Act
        var result = FilterAction.Validate(FilterOperator.GreaterThan, ComparisonType.Timestamp, "not-a-date");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not parseable");
    }

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithInvalidCombination_ReturnsFailure()
    {
        // Act
        var result = FilterAction.Create("col", FilterOperator.Contains, ComparisonType.Number, "1");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithValidCombination_ReturnsActionWithAllFields()
    {
        // Act
        var result = FilterAction.Create("col", FilterOperator.GreaterThan, ComparisonType.Number, "10");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var action = result.Value;
        action.ColumnName.Should().Be("col");
        action.Operator.Should().Be(FilterOperator.GreaterThan);
        action.ComparisonType.Should().Be(ComparisonType.Number);
        action.Value.Should().Be("10");
    }

    [Fact]
    public void Validate_UndefinedComparisonType_ReturnsFailure()
    {
        // Act — (ComparisonType)999 is not a defined enum value
        var result = FilterAction.Validate(FilterOperator.GreaterThan, (ComparisonType)999, "10");

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}

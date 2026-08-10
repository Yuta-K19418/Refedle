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
        var action = new FilterAction
        {
            ColumnName = "Price",
            Operator = FilterOperator.GreaterThan,
            Value = "100",
        };

        // Act
        var description = action.Description;

        // Assert
        description.Should().Be("Filter 'Price' GreaterThan '100'");
    }

}

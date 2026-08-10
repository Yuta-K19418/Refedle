using AwesomeAssertions;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.Engine.Models.Actions;

public sealed class FillColumnActionTests
{
    // -------------------------------------------------------------------------
    // Description property
    // -------------------------------------------------------------------------

    [Fact]
    public void Description_ReturnsExpectedFormat()
    {
        // Arrange
        var action = new FillColumnAction { ColumnName = "Email", Value = "REDACTED" };

        // Act
        var description = action.Description;

        // Assert
        description.Should().Be("Fill column 'Email' with 'REDACTED'");
    }

}

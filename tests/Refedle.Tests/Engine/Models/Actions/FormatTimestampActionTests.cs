using AwesomeAssertions;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.Engine.Models.Actions;

public sealed class FormatTimestampActionTests
{
    [Fact]
    public void Description_Property_Returns_Correct_String()
    {
        // Arrange
        var action = new FormatTimestampAction { ColumnName = "CreatedAt", TargetFormat = "yyyy-MM-dd" };

        // Act
        var description = action.Description;

        // Assert
        description.Should().Be("Format timestamp column 'CreatedAt' → \"yyyy-MM-dd\"");
    }

}

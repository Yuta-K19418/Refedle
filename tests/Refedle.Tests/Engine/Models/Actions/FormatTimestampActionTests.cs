using AwesomeAssertions;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.Engine.Models.Actions;

public sealed class FormatTimestampActionTests
{
    [Fact]
    public void Description_Property_Returns_Correct_String()
    {
        // Arrange
        FormatTimestampAction action = new() { ColumnName = "CreatedAt", TargetFormat = "yyyy-MM-dd" };

        // Act
        string description = action.Description;

        // Assert
        description.Should().Be("Format timestamp column 'CreatedAt' → \"yyyy-MM-dd\"");
    }

}

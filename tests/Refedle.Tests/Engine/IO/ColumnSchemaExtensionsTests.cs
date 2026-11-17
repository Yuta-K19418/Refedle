using AwesomeAssertions;
using Refedle.Engine.IO;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO;

public sealed class ColumnSchemaExtensionsTests
{
    [Theory]
    [InlineData(ColumnType.WholeNumber, ColumnType.WholeNumber, ColumnType.WholeNumber, false, "Same type")]
    [InlineData(ColumnType.Text, ColumnType.Text, ColumnType.Text, false, "Same type")]
    [InlineData(ColumnType.FloatingPoint, ColumnType.FloatingPoint, ColumnType.FloatingPoint, false, "Same type")]
    [InlineData(ColumnType.Boolean, ColumnType.Boolean, ColumnType.Boolean, false, "Same type")]
    [InlineData(ColumnType.Timestamp, ColumnType.Timestamp, ColumnType.Timestamp, false, "Same type")]
    [InlineData(ColumnType.WholeNumber, ColumnType.FloatingPoint, ColumnType.FloatingPoint, false, "WholeNumber -> FloatingPoint")]
    [InlineData(ColumnType.FloatingPoint, ColumnType.WholeNumber, ColumnType.FloatingPoint, false, "FloatingPoint -> WholeNumber (stays FloatingPoint)")]
    [InlineData(ColumnType.Boolean, ColumnType.WholeNumber, ColumnType.Text, false, "Boolean -> WholeNumber -> Text")]
    [InlineData(ColumnType.Boolean, ColumnType.FloatingPoint, ColumnType.Text, false, "Boolean -> FloatingPoint -> Text")]
    [InlineData(ColumnType.Timestamp, ColumnType.FloatingPoint, ColumnType.Text, false, "Timestamp -> FloatingPoint -> Text")]
    [InlineData(ColumnType.Timestamp, ColumnType.Text, ColumnType.Text, false, "Timestamp -> Text")]
    [InlineData(ColumnType.WholeNumber, ColumnType.Text, ColumnType.Text, false, "WholeNumber -> Text")]
    [InlineData(ColumnType.WholeNumber, ColumnType.Boolean, ColumnType.Text, false, "WholeNumber -> Boolean -> Text")]
    [InlineData(ColumnType.Text, ColumnType.WholeNumber, ColumnType.Text, false, "Text -> WholeNumber -> Text")]
    public void UpdateColumnType_VariousConversions_UpdatesCorrectly(
        ColumnType initialType,
        ColumnType newType,
        ColumnType expectedType,
        bool initialIsNullable,
        string description
    )
    {
        // Arrange
        ColumnSchema schema = new()
        {
            Name = "test",
            Type = initialType,
            IsNullable = initialIsNullable,
            ColumnIndex = 0,
        };

        // Act
        ColumnSchema updatedSchema = schema.WithUpdatedType(newType);

        // Assert
        updatedSchema.Type.Should().Be(expectedType, description);
        updatedSchema.IsNullable.Should().Be(initialIsNullable, "nullable state should not change");
        // Original schema should remain unchanged
        schema.Type.Should().Be(initialType, "original schema should not be modified");
        schema.IsNullable.Should().Be(initialIsNullable, "original nullable state should not change");
    }

    [Theory]
    [InlineData(ColumnType.WholeNumber)]
    [InlineData(ColumnType.FloatingPoint)]
    [InlineData(ColumnType.Boolean)]
    [InlineData(ColumnType.Timestamp)]
    [InlineData(ColumnType.Text)]
    public void WithMarkedNullable_SetsIsNullableToTrue(ColumnType columnType)
    {
        // Arrange
        ColumnSchema schema = new()
        {
            Name = "test",
            Type = columnType,
            IsNullable = false,
            ColumnIndex = 0,
        };

        // Act
        ColumnSchema updatedSchema = schema.WithMarkedNullable();

        // Assert
        updatedSchema.IsNullable.Should().BeTrue();
        updatedSchema.Type.Should().Be(columnType);
        // Original schema should remain unchanged
        schema.IsNullable.Should().BeFalse("original schema should not be modified");
    }

    [Theory]
    [InlineData(ColumnType.WholeNumber)]
    [InlineData(ColumnType.FloatingPoint)]
    [InlineData(ColumnType.Boolean)]
    [InlineData(ColumnType.Timestamp)]
    [InlineData(ColumnType.Text)]
    public void WithMarkedNullable_Idempotent_CallingTwiceReturnsSameInstance(ColumnType columnType)
    {
        // Arrange
        ColumnSchema schema = new()
        {
            Name = "test",
            Type = columnType,
            IsNullable = false,
            ColumnIndex = 0,
        };

        // Act
        ColumnSchema firstUpdated = schema.WithMarkedNullable();
        bool firstCall = firstUpdated.IsNullable;
        ColumnSchema secondUpdated = firstUpdated.WithMarkedNullable();
        bool secondCall = secondUpdated.IsNullable;

        // Assert
        firstCall.Should().BeTrue();
        secondCall.Should().BeTrue();
        // Should return same instance when already nullable
        secondUpdated.Should().BeSameAs(firstUpdated, "should return same instance when already nullable");
    }
}

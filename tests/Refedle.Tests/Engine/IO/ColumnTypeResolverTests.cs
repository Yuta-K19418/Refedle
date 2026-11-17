using AwesomeAssertions;
using Refedle.Engine.IO;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO;

public sealed class ColumnTypeResolverTests
{
    [Theory]
    // current = WholeNumber cases
    [InlineData(ColumnType.WholeNumber, ColumnType.WholeNumber, ColumnType.WholeNumber)]
    [InlineData(ColumnType.WholeNumber, ColumnType.FloatingPoint, ColumnType.FloatingPoint)]
    [InlineData(ColumnType.WholeNumber, ColumnType.Text, ColumnType.Text)]
    [InlineData(ColumnType.WholeNumber, ColumnType.Boolean, ColumnType.Text)]
    [InlineData(ColumnType.WholeNumber, ColumnType.Timestamp, ColumnType.Text)]
    // current = FloatingPoint cases
    [InlineData(ColumnType.FloatingPoint, ColumnType.FloatingPoint, ColumnType.FloatingPoint)]
    [InlineData(ColumnType.FloatingPoint, ColumnType.WholeNumber, ColumnType.FloatingPoint)]
    [InlineData(ColumnType.FloatingPoint, ColumnType.Text, ColumnType.Text)]
    [InlineData(ColumnType.FloatingPoint, ColumnType.Boolean, ColumnType.Text)]
    [InlineData(ColumnType.FloatingPoint, ColumnType.Timestamp, ColumnType.Text)]
    // current = Boolean cases
    [InlineData(ColumnType.Boolean, ColumnType.Boolean, ColumnType.Boolean)]
    [InlineData(ColumnType.Boolean, ColumnType.Text, ColumnType.Text)]
    [InlineData(ColumnType.Boolean, ColumnType.WholeNumber, ColumnType.Text)]
    [InlineData(ColumnType.Boolean, ColumnType.FloatingPoint, ColumnType.Text)]
    [InlineData(ColumnType.Boolean, ColumnType.Timestamp, ColumnType.Text)]
    // current = Timestamp cases
    [InlineData(ColumnType.Timestamp, ColumnType.Timestamp, ColumnType.Timestamp)]
    [InlineData(ColumnType.Timestamp, ColumnType.Text, ColumnType.Text)]
    [InlineData(ColumnType.Timestamp, ColumnType.WholeNumber, ColumnType.Text)]
    [InlineData(ColumnType.Timestamp, ColumnType.FloatingPoint, ColumnType.Text)]
    [InlineData(ColumnType.Timestamp, ColumnType.Boolean, ColumnType.Text)]
    // current = Text cases
    [InlineData(ColumnType.Text, ColumnType.Text, ColumnType.Text)]
    [InlineData(ColumnType.Text, ColumnType.WholeNumber, ColumnType.Text)]
    [InlineData(ColumnType.Text, ColumnType.FloatingPoint, ColumnType.Text)]
    [InlineData(ColumnType.Text, ColumnType.Boolean, ColumnType.Text)]
    [InlineData(ColumnType.Text, ColumnType.Timestamp, ColumnType.Text)]
    // current = JsonObject cases
    [InlineData(ColumnType.JsonObject, ColumnType.JsonObject, ColumnType.JsonObject)]
    [InlineData(ColumnType.JsonObject, ColumnType.JsonArray, ColumnType.Text)]
    [InlineData(ColumnType.JsonObject, ColumnType.Text, ColumnType.Text)]
    [InlineData(ColumnType.JsonObject, ColumnType.WholeNumber, ColumnType.Text)]
    [InlineData(ColumnType.JsonObject, ColumnType.FloatingPoint, ColumnType.Text)]
    // current = JsonArray cases
    [InlineData(ColumnType.JsonArray, ColumnType.JsonArray, ColumnType.JsonArray)]
    [InlineData(ColumnType.JsonArray, ColumnType.JsonObject, ColumnType.Text)]
    [InlineData(ColumnType.JsonArray, ColumnType.Text, ColumnType.Text)]
    [InlineData(ColumnType.JsonArray, ColumnType.WholeNumber, ColumnType.Text)]
    [InlineData(ColumnType.JsonArray, ColumnType.FloatingPoint, ColumnType.Text)]
    // Symmetric cases (observed = JsonObject/JsonArray with existing non-JSON type)
    [InlineData(ColumnType.WholeNumber, ColumnType.JsonObject, ColumnType.Text)]
    [InlineData(ColumnType.FloatingPoint, ColumnType.JsonArray, ColumnType.Text)]
    [InlineData(ColumnType.Boolean, ColumnType.JsonObject, ColumnType.Text)]
    [InlineData(ColumnType.Timestamp, ColumnType.JsonArray, ColumnType.Text)]
    public void Resolve_AllTypeCombinations_ReturnsExpectedResult(
        ColumnType current,
        ColumnType observed,
        ColumnType expected
    )
    {
        // Act
        ColumnType result = ColumnTypeResolver.Resolve(current, observed);

        // Assert
        result.Should().Be(expected);
    }
}


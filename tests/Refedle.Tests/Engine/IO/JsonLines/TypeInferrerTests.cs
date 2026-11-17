using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed class TypeInferrerTests
{
    [Theory]
    [InlineData("42", ColumnType.WholeNumber)]
    [InlineData("-9223372036854775808", ColumnType.WholeNumber)]
    [InlineData("9223372036854775807", ColumnType.WholeNumber)]
    [InlineData("3.14", ColumnType.FloatingPoint)]
    [InlineData("1.5e10", ColumnType.FloatingPoint)]
    [InlineData("9999999999999999999999", ColumnType.Text)]
    [InlineData("\"hello\"", ColumnType.Text)]
    [InlineData("\"\"", ColumnType.Text)]
    [InlineData("\"   \"", ColumnType.Text)]
    [InlineData("\"2024-01-01\"", ColumnType.Timestamp)]
    [InlineData("\"2024-01-01T12:00:00\"", ColumnType.Timestamp)]
    [InlineData("\"2024-01-01T12:00:00Z\"", ColumnType.Timestamp)]
    [InlineData("\"2024-01-01T12:00:00+09:00\"", ColumnType.Timestamp)]
    [InlineData("\"  2024-01-01  \"", ColumnType.Timestamp)]
    [InlineData("true", ColumnType.Boolean)]
    [InlineData("false", ColumnType.Boolean)]
    [InlineData("{}", ColumnType.JsonObject)]
    [InlineData("{\"a\":1}", ColumnType.JsonObject)]
    [InlineData("[]", ColumnType.JsonArray)]
    [InlineData("[1,2,3]", ColumnType.JsonArray)]
    public void InferType_ValueTokens_ReturnsExpectedType(string json, ColumnType expected)
    {
        // Arrange
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(bytes);
        reader.Read();

        // Act
        ColumnType result = TypeInferrer.InferType(reader.TokenType, reader.ValueSpan);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void InferType_NoneToken_ReturnsFallbackText()
    {
        // Arrange / Act
        ColumnType result = TypeInferrer.InferType(JsonTokenType.None, []);

        // Assert
        result.Should().Be(ColumnType.Text);
    }

    [Theory]
    [InlineData(JsonTokenType.Null, true)]
    [InlineData(JsonTokenType.String, false)]
    [InlineData(JsonTokenType.Number, false)]
    [InlineData(JsonTokenType.True, false)]
    [InlineData(JsonTokenType.False, false)]
    [InlineData(JsonTokenType.StartObject, false)]
    [InlineData(JsonTokenType.StartArray, false)]
    public void IsNullToken_TokenScenarios_ReturnsExpected(JsonTokenType tokenType, bool expected)
    {
        // Act
        bool result = TypeInferrer.IsNullToken(tokenType);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("2024-01-01", true)]
    [InlineData("2024-01-01T12:00:00", true)]
    [InlineData("2024-01-01T12:00:00Z", true)]
    [InlineData("2024-01-01T12:00:00+09:00", true)]
    [InlineData("2024-01-01T12:00:00.123456Z", true)]
    [InlineData("  2024-01-01  ", true)]
    [InlineData("\t2024-01-01\t", true)]
    [InlineData("\n2024-01-01\n", true)]
    [InlineData("hello", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("\t\t\t", false)]
    [InlineData("2024-13-01", false)]
    public void TryParseTimestamp_ValueScenarios_ReturnsExpected(string value, bool expected)
    {
        // Arrange
        byte[] bytes = Encoding.UTF8.GetBytes(value);

        // Act
        bool result = TypeInferrer.TryParseTimestamp(bytes, out _);

        // Assert
        result.Should().Be(expected);
    }
}

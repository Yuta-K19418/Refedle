using AwesomeAssertions;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.Csv;

public sealed class TypeInferrerTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData("  true  ", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("False", false)]
    [InlineData("  false  ", false)]
    public void TryParseBoolean_ValidValues_ReturnsTrue(string input, bool expected)
    {
        TypeInferrer.TryParseBoolean(input.AsSpan(), out bool result).Should().BeTrue();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("truee")]
    [InlineData("tru")]
    [InlineData("fals")]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseBoolean_InvalidValues_ReturnsFalse(string input)
    {
        TypeInferrer.TryParseBoolean(input.AsSpan(), out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("123", 123L)]
    [InlineData("456789", 456789L)]
    [InlineData("-123", -123L)]
    [InlineData("-456789", -456789L)]
    [InlineData("0", 0L)]
    [InlineData("-0", 0L)]
    [InlineData("  789  ", 789L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("-9223372036854775808", long.MinValue)]
    public void TryParseWholeNumber_ValidValues_ReturnsTrue(string input, long expected)
    {
        TypeInferrer.TryParseWholeNumber(input.AsSpan(), out long result).Should().BeTrue();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("12.34")]
    [InlineData("-5.6")]
    [InlineData("12abc")]
    [InlineData("abc123")]
    [InlineData("1.5e10")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseWholeNumber_InvalidValues_ReturnsFalse(string input)
    {
        TypeInferrer.TryParseWholeNumber(input.AsSpan(), out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("12.34", 12.34)]
    [InlineData("1,002.34", 1002.34)]
    [InlineData("-0.5", -0.5)]
    [InlineData("123", 123.0)]
    [InlineData("-456", -456.0)]
    [InlineData("1.5e10", 1.5e10)]
    [InlineData("-2.3E-5", -2.3e-5)]
    [InlineData("  3.14  ", 3.14)]
    public void TryParseFloatingPoint_ValidValues_ReturnsTrue(string input, double expected)
    {
        TypeInferrer.TryParseFloatingPoint(input.AsSpan(), out double result).Should().BeTrue();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("12.34abc")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseFloatingPoint_InvalidValues_ReturnsFalse(string input)
    {
        TypeInferrer.TryParseFloatingPoint(input.AsSpan(), out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void TryParseFloatingPoint_SpecialValues_ReturnsTrue(string input)
    {
        TypeInferrer.TryParseFloatingPoint(input.AsSpan(), out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("2024-01-15", 2024, 1, 15, 0, 0, 0, DateTimeKind.Unspecified)]
    [InlineData("2024-01-15T10:30:00", 2024, 1, 15, 10, 30, 0, DateTimeKind.Unspecified)]
    [InlineData("2024-01-15T10:30:00Z", 2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)]
    [InlineData("  2024-01-15  ", 2024, 1, 15, 0, 0, 0, DateTimeKind.Unspecified)]
    public void TryParseTimestamp_ValidValues_ReturnsTrue(
        string input,
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second,
        DateTimeKind kind
    )
    {
        TypeInferrer.TryParseTimestamp(input.AsSpan(), out DateTime result).Should().BeTrue();
        DateTime expected = new(year, month, day, hour, minute, second, kind);
        result.Date.Should().Be(expected.Date);
    }

    [Theory]
    [InlineData("invalid-date")]
    [InlineData("2024-13-45")]
    [InlineData("not-a-date")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseTimestamp_InvalidValues_ReturnsFalse(string input)
    {
        TypeInferrer.TryParseTimestamp(input.AsSpan(), out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("\t\n\r ", true)]
    [InlineData("text", false)]
    [InlineData("  text  ", false)]
    [InlineData("123", false)]
    public void IsEmptyOrWhitespace_DetectsCorrectly(string input, bool expected)
    {
        TypeInferrer.IsEmptyOrWhitespace(input.AsSpan()).Should().Be(expected);
    }

    [Theory]
    [InlineData("true", ColumnType.Boolean)]
    [InlineData("false", ColumnType.Boolean)]
    [InlineData("TRUE", ColumnType.Boolean)]
    public void InferType_PrioritizesBoolean(string input, ColumnType expected)
    {
        TypeInferrer.InferType(input.AsSpan()).Should().Be(expected);
    }

    [Theory]
    [InlineData("123", ColumnType.WholeNumber)]
    [InlineData("-456", ColumnType.WholeNumber)]
    [InlineData("0", ColumnType.WholeNumber)]
    public void InferType_PrioritizesWholeNumberOverFloatingPoint(string input, ColumnType expected)
    {
        TypeInferrer.InferType(input.AsSpan()).Should().Be(expected);
    }

    [Theory]
    [InlineData("12.34", ColumnType.FloatingPoint)]
    [InlineData("1,002.34", ColumnType.FloatingPoint)]
    [InlineData("-0.5", ColumnType.FloatingPoint)]
    [InlineData("1.5e10", ColumnType.FloatingPoint)]
    [InlineData("NaN", ColumnType.FloatingPoint)]
    public void InferType_DetectsFloatingPoint(string input, ColumnType expected)
    {
        TypeInferrer.InferType(input.AsSpan()).Should().Be(expected);
    }

    [Theory]
    [InlineData("2024-01-15", ColumnType.Timestamp)]
    [InlineData("2024-01-15T10:30:00", ColumnType.Timestamp)]
    [InlineData("1/15/2024", ColumnType.Timestamp)]
    public void InferType_DetectsTimestamp(string input, ColumnType expected)
    {
        TypeInferrer.InferType(input.AsSpan()).Should().Be(expected);
    }

    [Theory]
    [InlineData("hello world", ColumnType.Text)]
    [InlineData("abc123def", ColumnType.Text)]
    [InlineData("12.34.56", ColumnType.Text)]
    [InlineData("not-a-date-or-number", ColumnType.Text)]
    [InlineData("", ColumnType.Text)]
    [InlineData("   ", ColumnType.Text)]
    public void InferType_FallbackToText(string input, ColumnType expected)
    {
        TypeInferrer.InferType(input.AsSpan()).Should().Be(expected);
    }
}

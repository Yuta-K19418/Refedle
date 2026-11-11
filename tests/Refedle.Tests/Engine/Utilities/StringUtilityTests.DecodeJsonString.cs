using System.Buffers;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Refedle.Engine.Utilities;

namespace Refedle.Tests.Engine.Utilities;

public sealed partial class StringUtilityTests
{
    [Theory]
    [InlineData("\"value\"", "value")]
    [InlineData("\"hello こんにちは\"", "hello こんにちは")]
    [InlineData("\"\"", "")]
    public void DecodeJsonString_WithUnescapedToken_ReturnsTranscodedText(string json, string expected)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(json);

        // Act
        var result = Decode(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("\"line\\nbreak\"", "line\nbreak")]
    [InlineData("\"tab\\there\"", "tab\there")]
    [InlineData("\"back\\bspace\"", "back\bspace")]
    [InlineData("\"form\\ffeed\"", "form\ffeed")]
    [InlineData("\"carriage\\rreturn\"", "carriage\rreturn")]
    [InlineData("\"quote \\\"q\\\"\"", "quote \"q\"")]
    [InlineData("\"back\\\\slash\"", "back\\slash")]
    [InlineData("\"a\\/b\"", "a/b")]
    [InlineData("\"\\u0041\\u00e9\"", "Aé")]
    [InlineData("\"\\uD83D\\uDE00\"", "😀")]
    [InlineData("\"mix\\ttext こんにちは\"", "mix\ttext こんにちは")]
    public void DecodeJsonString_WithEscapedToken_ResolvesEscapeSequences(string json, string expected)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(json);

        // Act
        var result = Decode(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void DecodeJsonString_WithUnescapedInvalidUtf8_ReturnsReplacementText()
    {
        // Arrange: bytes '"a<0xFF>b"' — 0xFF is not valid UTF-8
        ReadOnlySpan<byte> json = [0x22, 0x61, 0xFF, 0x62, 0x22];

        // Act
        var result = Decode(json);

        // Assert
        result.Should().Be("a\uFFFDb");
    }

    [Fact]
    public void DecodeJsonString_WithUnpairedSurrogateEscape_ReturnsRawText()
    {
        // Arrange: "\uD800" is a lone high surrogate escape, which cannot be decoded
        var json = "\"\\uD800\""u8;

        // Act
        var result = Decode(json);

        // Assert
        result.Should().Be("\\uD800");
    }

    [Fact]
    public void DecodeJsonString_WithEscapedInvalidUtf8_ReturnsRawTextWithReplacement()
    {
        // Arrange: bytes '"a\n<0xFF>"' — escapes present but the token contains invalid UTF-8
        ReadOnlySpan<byte> json = [0x22, 0x61, 0x5C, 0x6E, 0xFF, 0x22];

        // Act
        var result = Decode(json);

        // Assert
        result.Should().Be("a\\n\uFFFD");
    }

    [Fact]
    public void DecodeJsonString_WithUnescapedTokenSpanningSegments_ThrowsNotSupportedException()
    {
        // Arrange: the unescaped string token "hello" is split across two segments
        var sequence = CreateSegmentedSequence("\"hel"u8.ToArray(), "lo\""u8.ToArray());

        // Act
        var act = () => Decode(sequence);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void DecodeJsonString_WithEscapedTokenSpanningSegments_ThrowsNotSupportedException()
    {
        // Arrange: the escaped token "a\n<0xFF>" is split across two segments
        var sequence = CreateSegmentedSequence("\"a\\n"u8.ToArray(), [0xFF, 0x22]);

        // Act
        var act = () => Decode(sequence);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void DecodeJsonString_WithTokenContainedInSingleSegment_ResolvesEscapeSequences()
    {
        // Arrange: the sequence has two segments but the token lies entirely in the second one
        var sequence = CreateSegmentedSequence(" "u8.ToArray(), "\"caf\\u00e9\""u8.ToArray());

        // Act
        var result = Decode(sequence);

        // Assert
        result.Should().Be("café");
    }

    private static string Decode(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json);
        reader.Read();
        return StringUtility.DecodeJsonString(ref reader);
    }

    private static string Decode(ReadOnlySequence<byte> json)
    {
        var reader = new Utf8JsonReader(json);
        reader.Read();
        return StringUtility.DecodeJsonString(ref reader);
    }

    private static ReadOnlySequence<byte> CreateSegmentedSequence(byte[] first, byte[] second)
    {
        var head = new SequenceSegment(first);
        var tail = head.Append(new SequenceSegment(second));
        return new ReadOnlySequence<byte>(head, 0, tail, tail.Memory.Length);
    }

    private sealed class SequenceSegment : ReadOnlySequenceSegment<byte>
    {
        // ReadOnlySequenceSegment<T>.Memory has a protected setter, so it must be assigned in a constructor body
        public SequenceSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public SequenceSegment Append(SequenceSegment next)
        {
            next.RunningIndex = RunningIndex + Memory.Length;
            Next = next;
            return next;
        }
    }
}

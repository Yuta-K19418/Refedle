using System.Text;
using AwesomeAssertions;
using Refedle.Engine.Utilities;

namespace Refedle.Tests.Engine.Utilities;

public sealed class Utf8BomUtilityTests
{
    public static IEnumerable<object[]> BomCases()
    {
        // Full UTF-8 BOM followed by content → BOM.
        yield return [(byte[])[0xEF, 0xBB, 0xBF, (byte)'{'], true];
        // Full UTF-8 BOM with nothing after it → BOM.
        yield return [(byte[])[0xEF, 0xBB, 0xBF], true];
        // Ordinary content of 3+ bytes → no BOM.
        yield return [Encoding.ASCII.GetBytes("abc"), false];
        // Near-miss — first two BOM bytes but the third differs (BE vs BF) → no BOM.
        yield return [(byte[])[0xEF, 0xBB, 0xBE], false];
        // Shorter than 3 bytes, even if a BOM prefix → no BOM.
        yield return [(byte[])[0xEF, 0xBB], false];
        yield return [(byte[])[0xEF], false];
        yield return [Array.Empty<byte>(), false];
    }

    [Theory]
    [MemberData(nameof(BomCases))]
    public void StartsWithUtf8Bom_VariousHeaders_ReturnsExpectedResult(byte[] bytes, bool expected)
    {
        // Arrange
        // (bytes supplied by the theory data)

        // Act
        var result = Utf8BomUtility.StartsWithUtf8Bom(bytes);

        // Assert
        result.Should().Be(expected);
    }

    public static IEnumerable<object[]> StreamCases()
    {
        // BOM followed by content → position 3.
        yield return [(byte[])[0xEF, 0xBB, 0xBF, (byte)'{'], 3L];
        // BOM only → position 3.
        yield return [(byte[])[0xEF, 0xBB, 0xBF], 3L];
        // No BOM → position 0.
        yield return [Encoding.ASCII.GetBytes("abc"), 0L];
        // Near-miss → position 0.
        yield return [(byte[])[0xEF, 0xBB, 0xBE], 0L];
        // Shorter than 3 bytes → position 0.
        yield return [Encoding.ASCII.GetBytes("ab"), 0L];
        yield return [(byte[])[0xEF], 0L];
        yield return [Array.Empty<byte>(), 0L];
    }

    [Theory]
    [MemberData(nameof(StreamCases))]
    public void SkipUtf8Bom_VariousStreams_LeavesStreamAtExpectedPosition(byte[] content, long expectedPosition)
    {
        // Arrange
        using var stream = new MemoryStream(content);

        // Act
        Utf8BomUtility.SkipUtf8Bom(stream);

        // Assert
        stream.Position.Should().Be(expectedPosition);
    }

    [Fact]
    public void SkipUtf8Bom_NonZeroStartPositionWithoutBom_RestoresStartPosition()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("xxabcd"));
        stream.Position = 2;

        // Act
        Utf8BomUtility.SkipUtf8Bom(stream);

        // Assert
        stream.Position.Should().Be(2);
    }

    [Fact]
    public void SkipUtf8Bom_NonZeroStartPositionWithBom_AdvancesPastBom()
    {
        // Arrange
        using var stream = new MemoryStream([(byte)'x', (byte)'x', 0xEF, 0xBB, 0xBF, (byte)'{']);
        stream.Position = 2;

        // Act
        Utf8BomUtility.SkipUtf8Bom(stream);

        // Assert
        stream.Position.Should().Be(5);
    }

    [Fact]
    public void SkipUtf8Bom_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        Stream stream = null!;

        // Act
        var act = () => Utf8BomUtility.SkipUtf8Bom(stream);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}

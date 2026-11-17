using System.Text;
using AwesomeAssertions;
using Refedle.Engine;
using Refedle.Engine.IO;

namespace Refedle.Tests.Engine.IO;

public sealed class MmapServiceTests : IDisposable
{
    private readonly string _testFilePath;
    private const string TestContent = "Hello, Refedle!";

    public MmapServiceTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
        File.WriteAllText(_testFilePath, TestContent);
    }

    public static IEnumerable<object[]> GetOffsetAfterUtf8BomCases()
    {
        // Full UTF-8 BOM (EF BB BF) followed by content → body starts at 3.
        yield return [(byte[])[0xEF, 0xBB, 0xBF, (byte)'{'], 3L];
        // Full UTF-8 BOM with nothing after it → body starts at 3 (EOF right after the BOM).
        yield return [(byte[])[0xEF, 0xBB, 0xBF], 3L];
        // No BOM — ordinary content of 3+ bytes → 0.
        yield return [Encoding.ASCII.GetBytes("abc"), 0L];
        // Near-miss — first two BOM bytes but the third differs (BE vs BF) → 0.
        yield return [(byte[])[0xEF, 0xBB, 0xBE], 0L];
        // File shorter than 3 bytes → 0.
        yield return [Encoding.ASCII.GetBytes("ab"), 0L];
        // Single BOM-prefix byte → 0.
        yield return [(byte[])[0xEF], 0L];
    }

    [Theory]
    [MemberData(nameof(GetOffsetAfterUtf8BomCases))]
    public void GetOffsetAfterUtf8Bom_VariousHeaders_ReturnsExpectedBodyOffset(byte[] header, long expected)
    {
        // Arrange
        File.WriteAllBytes(_testFilePath, header);
        using MmapService mmap = MmapService.Open(_testFilePath).Value;

        // Act
        long bodyOffset = mmap.GetOffsetAfterUtf8Bom();

        // Assert
        bodyOffset.Should().Be(expected);
    }

    [Fact]
    public void GetOffsetAfterUtf8Bom_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        MmapService mmap = MmapService.Open(_testFilePath).Value;
        mmap.Dispose();

        // Act
        Func<long> act = () => mmap.GetOffsetAfterUtf8Bom();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Open_ValidFile_ReturnsSuccess()
    {
        // Act
        Result<MmapService> result = MmapService.Open(_testFilePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using MmapService service = result.Value;
        service.Length.Should().Be(TestContent.Length);
    }

    [Fact]
    public void Open_NonExistentFile_ReturnsFailure()
    {
        // Act
        Result<MmapService> result = MmapService.Open("nonexistent.txt");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().ContainEquivalentOf("not found");
    }

    [Fact]
    public void Open_EmptyFile_ReturnsFailure()
    {
        // Arrange
        string emptyFilePath = Path.Combine(Path.GetTempPath(), $"empty_{Guid.NewGuid()}.txt");
        File.WriteAllText(emptyFilePath, string.Empty);

        try
        {
            // Act
            Result<MmapService> result = MmapService.Open(emptyFilePath);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().ContainEquivalentOf("empty");
        }
        finally
        {
            File.Delete(emptyFilePath);
        }
    }

    [Fact]
    public void Open_NullFilePath_ThrowsArgumentNullException()
    {
        // Act
        Func<Result<MmapService>> act = () => MmapService.Open(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Open_EmptyFilePath_ThrowsArgumentException()
    {
        // Act
        Func<Result<MmapService>> act = () => MmapService.Open(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Open_WhitespaceFilePath_ThrowsArgumentException()
    {
        // Act
        Func<Result<MmapService>> act = () => MmapService.Open("   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Read_ValidRange_ReadsCorrectData()
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;
        Span<byte> buffer = stackalloc byte[5];

        // Act
        service.Read(0, buffer);

        // Assert
        Encoding.UTF8.GetString(buffer).Should().Be("Hello");
    }

    [Fact]
    public void Read_FullFile_ReadsCorrectData()
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;
        Span<byte> buffer = stackalloc byte[TestContent.Length];

        // Act
        service.Read(0, buffer);

        // Assert
        Encoding.UTF8.GetString(buffer).Should().Be(TestContent);
    }

    [Theory]
    [InlineData(1, 5, "ello,")]
    [InlineData(7, 8, "Refedle!")]
    [InlineData(14, 1, "!")]
    public void Read_ValidNonZeroRange_CopiesRequestedBytes(int offset, int length, string expected)
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;
        Span<byte> buffer = stackalloc byte[length];

        // Act
        service.Read(offset, buffer);

        // Assert
        Encoding.UTF8.GetString(buffer).Should().Be(expected);
    }

    [Fact]
    public void Read_ZeroLength_DoesNotThrow()
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;

        // Act
        Action act = () => service.Read(0, []);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Read_OffsetAtEnd_ZeroLengthDoesNotThrow()
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;

        // Act
        Action act = () => service.Read(TestContent.Length, []);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Read_NegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;
        byte[] buffer = new byte[1];

        // Act
        Action act = () => service.Read(-1, buffer);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Read_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;
        byte[] buffer = new byte[TestContent.Length + 1];

        // Act
        Action act = () => service.Read(0, buffer);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Read_OffsetPlusLengthOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;
        byte[] buffer = new byte[2];

        // Act
        Action act = () => service.Read(TestContent.Length - 1, buffer);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Read_LargeOffsetAndLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;
        // Using values that would overflow if added: offset + length would wrap around
        long largeOffset = long.MaxValue - 100;
        byte[] buffer = new byte[1000];

        // Act
        Action act = () => service.Read(largeOffset, buffer);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TryRead_ValidRange_ReturnsSuccess()
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;
        Span<byte> buffer = stackalloc byte[5];

        // Act
        (bool success, string? error) = service.TryRead(0, buffer);

        // Assert
        success.Should().BeTrue();
        error.Should().BeEmpty();
        Encoding.UTF8.GetString(buffer).Should().Be("Hello");
    }

    [Fact]
    public void TryRead_NegativeOffset_ReturnsFailure()
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;
        Span<byte> buffer = stackalloc byte[1];

        // Act
        (bool success, string? error) = service.TryRead(-1, buffer);

        // Assert
        success.Should().BeFalse();
        error.Should().ContainEquivalentOf("non-negative");
    }

    [Fact]
    public void TryRead_OutOfRange_ReturnsFailure()
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;
        Span<byte> buffer = stackalloc byte[TestContent.Length + 1];

        // Act
        (bool success, string? error) = service.TryRead(0, buffer);

        // Assert
        success.Should().BeFalse();
        error.Should().ContainEquivalentOf("exceeds");
    }

    [Fact]
    public void TryRead_LargeOffsetAndLength_ReturnsFailure()
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;
        // Using values that would overflow if added: offset + length would wrap around
        long largeOffset = long.MaxValue - 100;
        byte[] buffer = new byte[1000];

        // Act
        (bool success, string? error) = service.TryRead(largeOffset, buffer);

        // Assert
        success.Should().BeFalse();
        error.Should().ContainEquivalentOf("exceeds");
    }

    [Fact]
    public void TryRead_AfterDispose_ReturnsFailure()
    {
        // Arrange
        MmapService service = MmapService.Open(_testFilePath).Value;
        service.Dispose();
        Span<byte> buffer = stackalloc byte[1];

        // Act
        (bool success, string? error) = service.TryRead(0, buffer);

        // Assert
        success.Should().BeFalse();
        error.Should().ContainEquivalentOf("disposed");
    }

    [Fact]
    public void Length_AfterOpen_ReturnsFileSize()
    {
        // Arrange
        using MmapService service = MmapService.Open(_testFilePath).Value;

        // Act
        long length = service.Length;

        // Assert
        length.Should().Be(TestContent.Length);
    }

    [Fact]
    public void Length_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        MmapService service = MmapService.Open(_testFilePath).Value;
        service.Dispose();

        // Act
        Func<long> act = () => service.Length;

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Read_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        MmapService service = MmapService.Open(_testFilePath).Value;
        service.Dispose();
        byte[] buffer = new byte[1];

        // Act
        Action act = () => service.Read(0, buffer);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        MmapService service = MmapService.Open(_testFilePath).Value;

        // Act
        Action act = () =>
        {
            service.Dispose();
            service.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }
}

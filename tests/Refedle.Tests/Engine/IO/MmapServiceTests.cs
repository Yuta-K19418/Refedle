using System.Text;
using AwesomeAssertions;
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

    [Fact]
    public void Open_ValidFile_ReturnsSuccess()
    {
        // Act
        var result = MmapService.Open(_testFilePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var service = result.Value;
        service.Length.Should().Be(TestContent.Length);
    }

    [Fact]
    public void Open_NonExistentFile_ReturnsFailure()
    {
        // Act
        var result = MmapService.Open("nonexistent.txt");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().ContainEquivalentOf("not found");
    }

    [Fact]
    public void Open_EmptyFile_ReturnsFailure()
    {
        // Arrange
        var emptyFilePath = Path.Combine(Path.GetTempPath(), $"empty_{Guid.NewGuid()}.txt");
        File.WriteAllText(emptyFilePath, string.Empty);

        try
        {
            // Act
            var result = MmapService.Open(emptyFilePath);

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
        var act = () => MmapService.Open(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Open_EmptyFilePath_ThrowsArgumentException()
    {
        // Act
        var act = () => MmapService.Open(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Open_WhitespaceFilePath_ThrowsArgumentException()
    {
        // Act
        var act = () => MmapService.Open("   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Read_ValidRange_ReadsCorrectData()
    {
        // Arrange
        using var service = MmapService.Open(_testFilePath).Value;
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
        using var service = MmapService.Open(_testFilePath).Value;
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
        using var service = MmapService.Open(_testFilePath).Value;
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
        using var service = MmapService.Open(_testFilePath).Value;
        var buffer = Span<byte>.Empty;

        // Act & Assert (should not throw)
        service.Read(0, buffer);
    }

    [Fact]
    public void Read_OffsetAtEnd_ZeroLengthDoesNotThrow()
    {
        // Arrange
        using var service = MmapService.Open(_testFilePath).Value;
        var buffer = Span<byte>.Empty;

        // Act & Assert (should not throw)
        service.Read(TestContent.Length, buffer);
    }

    [Fact]
    public void Read_NegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var service = MmapService.Open(_testFilePath).Value;
        var buffer = new byte[1];

        // Act
        var act = () => service.Read(-1, buffer);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Read_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var service = MmapService.Open(_testFilePath).Value;
        var buffer = new byte[TestContent.Length + 1];

        // Act
        var act = () => service.Read(0, buffer);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Read_OffsetPlusLengthOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var service = MmapService.Open(_testFilePath).Value;
        var buffer = new byte[2];

        // Act
        var act = () => service.Read(TestContent.Length - 1, buffer);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Read_LargeOffsetAndLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var service = MmapService.Open(_testFilePath).Value;
        // Using values that would overflow if added: offset + length would wrap around
        var largeOffset = long.MaxValue - 100;
        var buffer = new byte[1000];

        // Act
        var act = () => service.Read(largeOffset, buffer);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TryRead_ValidRange_ReturnsSuccess()
    {
        // Arrange
        using var service = MmapService.Open(_testFilePath).Value;
        Span<byte> buffer = stackalloc byte[5];

        // Act
        var (success, error) = service.TryRead(0, buffer);

        // Assert
        success.Should().BeTrue();
        error.Should().BeEmpty();
        Encoding.UTF8.GetString(buffer).Should().Be("Hello");
    }

    [Fact]
    public void TryRead_NegativeOffset_ReturnsFailure()
    {
        // Arrange
        using var service = MmapService.Open(_testFilePath).Value;
        Span<byte> buffer = stackalloc byte[1];

        // Act
        var (success, error) = service.TryRead(-1, buffer);

        // Assert
        success.Should().BeFalse();
        error.Should().ContainEquivalentOf("non-negative");
    }

    [Fact]
    public void TryRead_OutOfRange_ReturnsFailure()
    {
        // Arrange
        using var service = MmapService.Open(_testFilePath).Value;
        Span<byte> buffer = stackalloc byte[TestContent.Length + 1];

        // Act
        var (success, error) = service.TryRead(0, buffer);

        // Assert
        success.Should().BeFalse();
        error.Should().ContainEquivalentOf("exceeds");
    }

    [Fact]
    public void TryRead_LargeOffsetAndLength_ReturnsFailure()
    {
        // Arrange
        using var service = MmapService.Open(_testFilePath).Value;
        // Using values that would overflow if added: offset + length would wrap around
        var largeOffset = long.MaxValue - 100;
        var buffer = new byte[1000];

        // Act
        var (success, error) = service.TryRead(largeOffset, buffer);

        // Assert
        success.Should().BeFalse();
        error.Should().ContainEquivalentOf("exceeds");
    }

    [Fact]
    public void TryRead_AfterDispose_ReturnsFailure()
    {
        // Arrange
        var service = MmapService.Open(_testFilePath).Value;
        service.Dispose();
        Span<byte> buffer = stackalloc byte[1];

        // Act
        var (success, error) = service.TryRead(0, buffer);

        // Assert
        success.Should().BeFalse();
        error.Should().ContainEquivalentOf("disposed");
    }

    [Fact]
    public void Length_AfterOpen_ReturnsFileSize()
    {
        // Arrange
        using var service = MmapService.Open(_testFilePath).Value;

        // Act
        var length = service.Length;

        // Assert
        length.Should().Be(TestContent.Length);
    }

    [Fact]
    public void Length_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var service = MmapService.Open(_testFilePath).Value;
        service.Dispose();

        // Act
        var act = () => service.Length;

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Read_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var service = MmapService.Open(_testFilePath).Value;
        service.Dispose();
        var buffer = new byte[1];

        // Act
        var act = () => service.Read(0, buffer);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var service = MmapService.Open(_testFilePath).Value;

        // Act
        var act = () =>
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

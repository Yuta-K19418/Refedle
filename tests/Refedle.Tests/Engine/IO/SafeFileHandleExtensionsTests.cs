using System.Text;
using AwesomeAssertions;
using Refedle.Engine.IO;

namespace Refedle.Tests.Engine.IO;

public sealed class SafeFileHandleExtensionsTests() : IDisposable
{
    private readonly string _testDir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())).FullName;

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    public static IEnumerable<object[]> OffsetCases()
    {
        // BOM followed by content → body starts after the BOM.
        yield return [(byte[])[0xEF, 0xBB, 0xBF, (byte)'{'], 3L];
        // BOM only → body starts after the BOM.
        yield return [(byte[])[0xEF, 0xBB, 0xBF], 3L];
        // Ordinary content of 3+ bytes → no BOM.
        yield return [Encoding.ASCII.GetBytes("abc"), 0L];
        // Shorter than 3 bytes, even if a BOM prefix → no BOM.
        yield return [Encoding.ASCII.GetBytes("ab"), 0L];
        yield return [(byte[])[0xEF], 0L];
        // Empty file → no BOM.
        yield return [Array.Empty<byte>(), 0L];
    }

    [Theory]
    [MemberData(nameof(OffsetCases))]
    public void GetOffsetAfterUtf8Bom_VariousFiles_ReturnsExpectedOffset(byte[] content, long expectedOffset)
    {
        // Arrange
        var filePath = Path.Combine(_testDir, Path.GetRandomFileName());
        File.WriteAllBytes(filePath, content);
        using var handle = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act
        var offset = handle.GetOffsetAfterUtf8Bom();

        // Assert
        offset.Should().Be(expectedOffset);
    }
}

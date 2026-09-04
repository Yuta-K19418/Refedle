using System.Runtime.Versioning;
using AwesomeAssertions;
using Refedle.App.Cli.Update;

namespace Refedle.Tests.App.Cli.Update;

public sealed class ArchiveBinaryReplacerTests
{
    private const UnixFileMode ExpectedExecutableMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private static readonly byte[] NewBinaryBytes = "new refedle binary v1.2.3"u8.ToArray();

    [Fact]
    public async Task ReplaceAsync_WithValidArchive_ReplacesTargetAndLeavesNoTempFile()
    {
        // Arrange
        using var fixture = UpdateArchiveFixture.WithBinaryEntry(NewBinaryBytes);
        var replacer = new ArchiveBinaryReplacer();

        // Act
        var result = await replacer.ReplaceAsync(fixture.ArchivePath, fixture.TargetPath, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await File.ReadAllBytesAsync(fixture.TargetPath)).Should().Equal(NewBinaryBytes);
        fixture.TargetDirectory.GetFiles("*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_WithCorruptGzip_PreservesTargetAndReturnsFailure()
    {
        // Arrange
        using var fixture = UpdateArchiveFixture.WithCorruptGzip();
        var replacer = new ArchiveBinaryReplacer();

        // Act
        var act = async () => await replacer.ReplaceAsync(fixture.ArchivePath, fixture.TargetPath, CancellationToken.None);

        // Assert
        var result = (await act.Should().NotThrowAsync()).Which;
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("corrupted");
        (await File.ReadAllBytesAsync(fixture.TargetPath)).Should().Equal(UpdateArchiveFixture.OriginalTargetBytes);
        fixture.TargetDirectory.GetFiles("*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_WithoutBinaryEntry_PreservesTargetAndReturnsFailure()
    {
        // Arrange
        using var fixture = UpdateArchiveFixture.WithoutBinaryEntry();
        var replacer = new ArchiveBinaryReplacer();

        // Act
        var act = async () => await replacer.ReplaceAsync(fixture.ArchivePath, fixture.TargetPath, CancellationToken.None);

        // Assert
        var result = (await act.Should().NotThrowAsync()).Which;
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not contain the 'refedle' binary");
        (await File.ReadAllBytesAsync(fixture.TargetPath)).Should().Equal(UpdateArchiveFixture.OriginalTargetBytes);
        fixture.TargetDirectory.GetFiles("*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_WhenTargetDirectoryDoesNotExist_ReturnsFailure()
    {
        // Arrange
        using var fixture = UpdateArchiveFixture.WithBinaryEntry(NewBinaryBytes);
        var missingTarget = Path.Combine(fixture.RootPath, "no-such-dir", "refedle");
        var replacer = new ArchiveBinaryReplacer();

        // Act
        var result = await replacer.ReplaceAsync(fixture.ArchivePath, missingTarget, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("target directory does not exist");
    }

    [Fact]
    public async Task ReplaceAsync_WhenCancelledBeforeMove_PreservesTargetAndThrows()
    {
        // Arrange
        using var fixture = UpdateArchiveFixture.WithBinaryEntry(NewBinaryBytes);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var replacer = new ArchiveBinaryReplacer();

        // Act
        var act = async () => await replacer.ReplaceAsync(fixture.ArchivePath, fixture.TargetPath, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        (await File.ReadAllBytesAsync(fixture.TargetPath)).Should().Equal(UpdateArchiveFixture.OriginalTargetBytes);
        fixture.TargetDirectory.GetFiles("*.tmp").Should().BeEmpty();
    }

    [UnixFact]
    [UnsupportedOSPlatform("windows")]
    public async Task ReplaceAsync_OnUnix_MarksReplacedBinaryExecutable()
    {
        // Arrange
        using var fixture = UpdateArchiveFixture.WithBinaryEntry(NewBinaryBytes);
        var replacer = new ArchiveBinaryReplacer();

        // Act
        var result = await replacer.ReplaceAsync(fixture.ArchivePath, fixture.TargetPath, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.GetUnixFileMode(fixture.TargetPath).Should().Be(ExpectedExecutableMode);
    }
}

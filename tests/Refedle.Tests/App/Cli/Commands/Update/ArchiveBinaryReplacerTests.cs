using System.Runtime.Versioning;
using AwesomeAssertions;
using Refedle.App.Cli.Commands.Update;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli.Commands.Update;

public sealed class ArchiveBinaryReplacerTests
{
    private const UnixFileMode ExpectedExecutableMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private static readonly byte[] _newBinaryBytes = "new refedle binary v1.2.3"u8.ToArray();

    [Fact]
    public async Task ReplaceAsync_WithValidArchive_ReplacesTargetAndLeavesNoTempFile()
    {
        // Arrange
        using UpdateArchiveFixture fixture = UpdateArchiveFixture.WithBinaryEntry(_newBinaryBytes);
        ArchiveBinaryReplacer replacer = new();

        // Act
        Result result = await replacer.ReplaceAsync(fixture.ArchivePath, fixture.TargetPath, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await File.ReadAllBytesAsync(fixture.TargetPath)).Should().Equal(_newBinaryBytes);
        fixture.TargetDirectory.GetFiles("*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_WithCorruptGzip_PreservesTargetAndReturnsFailure()
    {
        // Arrange
        using UpdateArchiveFixture fixture = UpdateArchiveFixture.WithCorruptGzip();
        ArchiveBinaryReplacer replacer = new();

        // Act
        Func<Task<Result>> act = async () => await replacer.ReplaceAsync(fixture.ArchivePath, fixture.TargetPath, CancellationToken.None);

        // Assert
        Result result = (await act.Should().NotThrowAsync()).Which;
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("corrupted");
        (await File.ReadAllBytesAsync(fixture.TargetPath)).Should().Equal(UpdateArchiveFixture.OriginalTargetBytes);
        fixture.TargetDirectory.GetFiles("*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_WithoutBinaryEntry_PreservesTargetAndReturnsFailure()
    {
        // Arrange
        using UpdateArchiveFixture fixture = UpdateArchiveFixture.WithoutBinaryEntry();
        ArchiveBinaryReplacer replacer = new();

        // Act
        Func<Task<Result>> act = async () => await replacer.ReplaceAsync(fixture.ArchivePath, fixture.TargetPath, CancellationToken.None);

        // Assert
        Result result = (await act.Should().NotThrowAsync()).Which;
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not contain the 'refedle' binary");
        (await File.ReadAllBytesAsync(fixture.TargetPath)).Should().Equal(UpdateArchiveFixture.OriginalTargetBytes);
        fixture.TargetDirectory.GetFiles("*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_WhenTargetDirectoryDoesNotExist_ReturnsFailure()
    {
        // Arrange
        using UpdateArchiveFixture fixture = UpdateArchiveFixture.WithBinaryEntry(_newBinaryBytes);
        string missingTarget = Path.Combine(fixture.RootPath, "no-such-dir", "refedle");
        ArchiveBinaryReplacer replacer = new();

        // Act
        Result result = await replacer.ReplaceAsync(fixture.ArchivePath, missingTarget, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("target directory does not exist");
    }

    [Fact]
    public async Task ReplaceAsync_WhenCancelledBeforeMove_PreservesTargetAndThrows()
    {
        // Arrange
        using UpdateArchiveFixture fixture = UpdateArchiveFixture.WithBinaryEntry(_newBinaryBytes);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        ArchiveBinaryReplacer replacer = new();

        // Act
        Func<Task<Result>> act = async () => await replacer.ReplaceAsync(fixture.ArchivePath, fixture.TargetPath, cts.Token);

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
        using UpdateArchiveFixture fixture = UpdateArchiveFixture.WithBinaryEntry(_newBinaryBytes);
        ArchiveBinaryReplacer replacer = new();

        // Act
        Result result = await replacer.ReplaceAsync(fixture.ArchivePath, fixture.TargetPath, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.GetUnixFileMode(fixture.TargetPath).Should().Be(ExpectedExecutableMode);
    }
}

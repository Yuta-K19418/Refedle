using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.App.Cli.Update;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli.Update;

public sealed class UpdateCommandTests
{
    private const string LinuxX64 = "linux-x64";
    private const string ArchiveName = "refedle-v0.3.0-linux-x64.tar.gz";
    private const string ChecksumsName = "checksums.txt";

    private static readonly byte[] ArchiveBytes = [1, 2, 3, 4, 5];

    [Fact]
    public async Task RunAsync_WithDevelopmentBuild_SkipsUpdateAndReturnsSuccess()
    {
        // Arrange
        var releaseClient = new FakeReleaseClient(Results.Success("v9.9.9"));
        var replacer = new FakeBinaryReplacer(Results.Success());
        var logger = new TestAppLogger();
        var command = new UpdateCommand("0.0.0-dev", releaseClient, replacer, RidStub(), logger);

        // Act
        var exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        releaseClient.GetLatestTagCallCount.Should().Be(0);
        replacer.CallCount.Should().Be(0);
        logger.Infos.Should().ContainSingle().Which.Should().Contain("development build");
    }

    [Theory]
    [InlineData("0.3.0", "v0.3.0", "v0.3.0")]
    [InlineData("v0.4.0", "0.3.0", "v0.4.0")]
    public async Task RunAsync_WhenCurrentIsAtLeastLatest_ReportsAlreadyUpToDateWithoutDownloading(
        string currentVersion,
        string latestTag,
        string expectedCurrentDisplay)
    {
        // Arrange
        var releaseClient = new FakeReleaseClient(Results.Success(latestTag));
        var replacer = new FakeBinaryReplacer(Results.Success());
        var logger = new TestAppLogger();
        var command = new UpdateCommand(currentVersion, releaseClient, replacer, RidStub(), logger);

        // Act
        var exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        releaseClient.DownloadedFileNames.Should().BeEmpty();
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().BeEmpty();
        logger.Infos.Should().Equal(
            $"Current version: {expectedCurrentDisplay}",
            "Latest version:  v0.3.0",
            string.Empty,
            "Already up to date.");
    }

    [Fact]
    public async Task RunAsync_WhenNewerVersionAvailable_RequestsAssetsInOrderAndWritesExactProgress()
    {
        // Arrange
        var checksums = Encoding.UTF8.GetBytes($"{Sha256Hex(ArchiveBytes)}  {ArchiveName}\n");
        var releaseClient = new FakeReleaseClient(Results.Success("v0.3.0"), checksums, ArchiveBytes);
        var replacer = new FakeBinaryReplacer(Results.Success());
        var logger = new TestAppLogger();
        var command = new UpdateCommand("0.2.0", releaseClient, replacer, RidStub(), logger);

        // Act
        var exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        releaseClient.DownloadRequests.Select(request => (request.Tag, request.FileName)).Should().Equal(
            ("v0.3.0", ChecksumsName),
            ("v0.3.0", ArchiveName));
        replacer.CallCount.Should().Be(1);
        replacer.ReceivedArchivePath.Should().EndWith(ArchiveName);
        replacer.ReceivedTargetPath.Should().Be(Environment.ProcessPath);
        logger.Errors.Should().BeEmpty();
        logger.Infos.Should().Equal(
            "Current version: v0.2.0",
            "Latest version:  v0.3.0",
            string.Empty,
            "Downloading refedle v0.3.0...",
            "Verifying checksum...",
            "Updated successfully: v0.2.0 -> v0.3.0");
    }

    [Fact]
    public async Task RunAsync_WhenCurrentVersionIsInvalid_ReturnsFailureWithoutContactingGitHub()
    {
        // Arrange
        var releaseClient = new FakeReleaseClient(Results.Success("v0.3.0"));
        var replacer = new FakeBinaryReplacer(Results.Success());
        var logger = new TestAppLogger();
        var command = new UpdateCommand("not-a-version", releaseClient, replacer, RidStub(), logger);

        // Act
        var exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        releaseClient.GetLatestTagCallCount.Should().Be(0);
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().ContainSingle().Which.Should().Contain("Invalid current version");
    }

    [Fact]
    public async Task RunAsync_WhenLatestTagCannotBeResolved_ReturnsFailure()
    {
        // Arrange
        var releaseClient = new FakeReleaseClient(
            Results.Failure<string>("Could not resolve the latest release tag (status: 500)."));
        var replacer = new FakeBinaryReplacer(Results.Success());
        var logger = new TestAppLogger();
        var command = new UpdateCommand("0.2.0", releaseClient, replacer, RidStub(), logger);

        // Act
        var exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        releaseClient.DownloadedFileNames.Should().BeEmpty();
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().ContainSingle().Which.Should().Contain("Could not resolve the latest release tag");
    }

    [Fact]
    public async Task RunAsync_WhenLatestTagIsNotSemver_ReturnsFailureWithoutDownloading()
    {
        // Arrange
        var releaseClient = new FakeReleaseClient(Results.Success("nightly"));
        var replacer = new FakeBinaryReplacer(Results.Success());
        var logger = new TestAppLogger();
        var command = new UpdateCommand("0.2.0", releaseClient, replacer, RidStub(), logger);

        // Act
        var exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        releaseClient.DownloadedFileNames.Should().BeEmpty();
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().ContainSingle().Which.Should().Contain("Could not parse the latest release tag");
    }

    [Fact]
    public async Task RunAsync_WhenRuntimeIdentifierIsUnsupported_ReturnsFailureWithoutDownloadingOrReplacing()
    {
        // Arrange
        var releaseClient = new FakeReleaseClient(Results.Success("v0.3.0"));
        var replacer = new FakeBinaryReplacer(Results.Success());
        var logger = new TestAppLogger();
        var rid = new StubRuntimeIdentifierResolver(Results.Failure<string>("Windows is not supported by 'refedle update'."));
        var command = new UpdateCommand("0.2.0", releaseClient, replacer, rid, logger);

        // Act
        var exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        releaseClient.DownloadedFileNames.Should().BeEmpty();
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().ContainSingle().Which.Should().Contain("Windows is not supported");
    }

    [Fact]
    public async Task RunAsync_WhenChecksumsAssetIsMissing_ReturnsFailureWithoutDownloadingArchiveOrReplacing()
    {
        // Arrange
        var releaseClient = new FakeReleaseClient(
            Results.Success("v0.3.0"),
            checksumsFailure: Results.Failure("Could not download 'checksums.txt' (status: 404 NotFound)."));
        var replacer = new FakeBinaryReplacer(Results.Success());
        var logger = new TestAppLogger();
        var command = new UpdateCommand("0.2.0", releaseClient, replacer, RidStub(), logger);

        // Act
        var exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        releaseClient.DownloadedFileNames.Should().Equal(ChecksumsName);
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().ContainSingle().Which.Should().Contain("404");
    }

    [Fact]
    public async Task RunAsync_WhenArchiveDownloadFails_ReturnsFailureWithoutReplacing()
    {
        // Arrange
        var checksums = Encoding.UTF8.GetBytes($"{Sha256Hex(ArchiveBytes)}  {ArchiveName}\n");
        var releaseClient = new FakeReleaseClient(
            Results.Success("v0.3.0"),
            checksums,
            archiveFailure: Results.Failure("Could not download 'refedle-v0.3.0-linux-x64.tar.gz' (status: 404 NotFound)."));
        var replacer = new FakeBinaryReplacer(Results.Success());
        var logger = new TestAppLogger();
        var command = new UpdateCommand("0.2.0", releaseClient, replacer, RidStub(), logger);

        // Act
        var exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        releaseClient.DownloadedFileNames.Should().Equal(ChecksumsName, ArchiveName);
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().ContainSingle().Which.Should().Contain("Could not download");
    }

    [Fact]
    public async Task RunAsync_WhenChecksumDoesNotMatch_ReturnsFailureWithoutReplacing()
    {
        // Arrange
        var wrongChecksums = Encoding.UTF8.GetBytes($"{new string('a', 64)}  {ArchiveName}\n");
        var releaseClient = new FakeReleaseClient(Results.Success("v0.3.0"), wrongChecksums, ArchiveBytes);
        var replacer = new FakeBinaryReplacer(Results.Success());
        var logger = new TestAppLogger();
        var command = new UpdateCommand("0.2.0", releaseClient, replacer, RidStub(), logger);

        // Act
        var exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().ContainSingle().Which.Should().Contain("Checksum mismatch");
    }

    [Fact]
    public async Task RunAsync_WhenBinaryReplacerFails_ReturnsFailure()
    {
        // Arrange
        var checksums = Encoding.UTF8.GetBytes($"{Sha256Hex(ArchiveBytes)}  {ArchiveName}\n");
        var releaseClient = new FakeReleaseClient(Results.Success("v0.3.0"), checksums, ArchiveBytes);
        var replacer = new FakeBinaryReplacer(Results.Failure("The archive does not contain the 'refedle' binary."));
        var logger = new TestAppLogger();
        var command = new UpdateCommand("0.2.0", releaseClient, replacer, RidStub(), logger);

        // Act
        var exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        replacer.CallCount.Should().Be(1);
        logger.Errors.Should().ContainSingle().Which.Should().Contain("does not contain the 'refedle' binary");
    }

    [Theory]
    [InlineData(UpdateFailureKind.Network, "Network error while contacting GitHub")]
    [InlineData(UpdateFailureKind.Timeout, "Network error while contacting GitHub")]
    [InlineData(UpdateFailureKind.FileIo, "File error during update")]
    [InlineData(UpdateFailureKind.CorruptArchive, "The downloaded archive is corrupted")]
    [InlineData(UpdateFailureKind.PermissionDenied, "Permission denied during update")]
    public async Task RunAsync_WhenInfrastructureThrows_ReturnsFailureWithCleanMessage(
        UpdateFailureKind exceptionKind,
        string expectedMessagePrefix)
    {
        // Arrange
        var releaseClient = new ThrowingReleaseClient(CreateException(exceptionKind));
        var replacer = new FakeBinaryReplacer(Results.Success());
        var logger = new TestAppLogger();
        var command = new UpdateCommand("0.2.0", releaseClient, replacer, RidStub(), logger);

        // Act
        var exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().ContainSingle().Which.Should().StartWith(expectedMessagePrefix);
    }

    [Fact]
    public async Task RunAsync_WhenCancellationRequested_ReturnsFailureWithCancelledMessage()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var releaseClient = new ThrowingReleaseClient(new OperationCanceledException(cts.Token));
        var replacer = new FakeBinaryReplacer(Results.Success());
        var logger = new TestAppLogger();
        var command = new UpdateCommand("0.2.0", releaseClient, replacer, RidStub(), logger);

        // Act
        var exitCode = await command.RunAsync(cts.Token);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Update cancelled.");
    }

    private static StubRuntimeIdentifierResolver RidStub() => new(Results.Success(LinuxX64));

    private static string Sha256Hex(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

    private static Exception CreateException(UpdateFailureKind kind) => kind switch
    {
        UpdateFailureKind.Network => new HttpRequestException("connection reset"),
        UpdateFailureKind.Timeout => new TaskCanceledException("the request timed out"),
        UpdateFailureKind.FileIo => new IOException("the disk is full"),
        UpdateFailureKind.CorruptArchive => new InvalidDataException("invalid gzip header"),
        UpdateFailureKind.PermissionDenied => new UnauthorizedAccessException("access to the path is denied"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

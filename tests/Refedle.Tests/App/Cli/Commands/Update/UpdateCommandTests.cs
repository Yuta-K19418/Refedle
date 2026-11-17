using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Cli.Commands.Update;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli.Commands.Update;

public sealed class UpdateCommandTests
{
    private const string LinuxX64 = "linux-x64";
    private const string ArchiveName = "refedle-v0.3.0-linux-x64.tar.gz";
    private const string ChecksumsName = "checksums.txt";

    private static readonly byte[] _archiveBytes = [1, 2, 3, 4, 5];

    [Fact]
    public async Task RunAsync_WithDevelopmentBuild_SkipsUpdateAndReturnsSuccess()
    {
        // Arrange
        FakeReleaseClient releaseClient = new(Results.Success("v9.9.9"));
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();
        UpdateCommand command = new("0.0.0-dev", releaseClient, replacer, RidStub(), logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

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
        FakeReleaseClient releaseClient = new(Results.Success(latestTag));
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();
        UpdateCommand command = new(currentVersion, releaseClient, replacer, RidStub(), logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

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
    public async Task RunAsync_WhenNewerVersionAvailable_RequestsAssetsInOrderAndWritesExpectedLogMessages()
    {
        // Arrange
        byte[] checksums = Encoding.UTF8.GetBytes($"{Sha256Hex(_archiveBytes)}  {ArchiveName}\n");
        FakeReleaseClient releaseClient = new(Results.Success("v0.3.0"), checksums, _archiveBytes);
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();
        UpdateCommand command = new("0.2.0", releaseClient, replacer, RidStub(), logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

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
            "Updated successfully: v0.2.0 -> v0.3.0");
    }

    [Fact]
    public async Task RunAsync_WhenNewerVersionAvailable_ReportsDownloadingVerifyingReplacingPhases()
    {
        // Arrange
        byte[] checksums = Encoding.UTF8.GetBytes($"{Sha256Hex(_archiveBytes)}  {ArchiveName}\n");
        FakeReleaseClient releaseClient = new(Results.Success("v0.3.0"), checksums, _archiveBytes);
        FakeBinaryReplacer replacer = new(Results.Success());
        TestStatusReporter status = new();
        UpdateCommand command = new("0.2.0", releaseClient, replacer, RidStub(), new TestAppLogger(), status);

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        status.Phases.Should().Equal("Downloading...", "Verifying checksum...", "Replacing binary...");
    }

    [Fact]
    public async Task RunAsync_WhenChecksumMismatches_StopsBeforeReplacingPhase()
    {
        // Arrange
        byte[] wrongChecksums = Encoding.UTF8.GetBytes($"{new string('a', 64)}  {ArchiveName}\n");
        FakeReleaseClient releaseClient = new(Results.Success("v0.3.0"), wrongChecksums, _archiveBytes);
        TestStatusReporter status = new();
        UpdateCommand command = new(
            "0.2.0", releaseClient, new FakeBinaryReplacer(Results.Success()), RidStub(), new TestAppLogger(), status);

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        status.Phases.Should().Equal("Downloading...", "Verifying checksum...");
    }

    [Theory]
    [InlineData("0.0.0-dev", "v9.9.9")]
    [InlineData("0.3.0", "v0.3.0")]
    public async Task RunAsync_WhenNothingToInstall_ShowsNoStatus(string currentVersion, string latestTag)
    {
        // Arrange
        FakeReleaseClient releaseClient = new(Results.Success(latestTag));
        TestStatusReporter status = new();
        UpdateCommand command = new(
            currentVersion, releaseClient, new FakeBinaryReplacer(Results.Success()), RidStub(), new TestAppLogger(), status);

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        status.Phases.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullStatusReporter_ThrowsArgumentNullException()
    {
        // Arrange
        FakeReleaseClient releaseClient = new(Results.Success("v0.3.0"));
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();

        // Act
        Func<UpdateCommand> act = () => new UpdateCommand("0.2.0", releaseClient, replacer, RidStub(), logger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("statusReporter");
    }

    [Fact]
    public async Task RunAsync_WhenCurrentVersionIsInvalid_ReturnsFailureWithoutContactingGitHub()
    {
        // Arrange
        FakeReleaseClient releaseClient = new(Results.Success("v0.3.0"));
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();
        UpdateCommand command = new("not-a-version", releaseClient, replacer, RidStub(), logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

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
        FakeReleaseClient releaseClient = new(
            Results.Failure<string>("Could not resolve the latest release tag (status: 500)."));
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();
        UpdateCommand command = new("0.2.0", releaseClient, replacer, RidStub(), logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

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
        FakeReleaseClient releaseClient = new(Results.Success("nightly"));
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();
        UpdateCommand command = new("0.2.0", releaseClient, replacer, RidStub(), logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

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
        FakeReleaseClient releaseClient = new(Results.Success("v0.3.0"));
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();
        StubRuntimeIdentifierResolver rid = new(Results.Failure<string>("Windows is not supported by 'refedle update'."));
        UpdateCommand command = new("0.2.0", releaseClient, replacer, rid, logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

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
        FakeReleaseClient releaseClient = new(
            Results.Success("v0.3.0"),
            checksumsFailure: Results.Failure("Could not download 'checksums.txt' (status: 404 NotFound)."));
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();
        UpdateCommand command = new("0.2.0", releaseClient, replacer, RidStub(), logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

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
        byte[] checksums = Encoding.UTF8.GetBytes($"{Sha256Hex(_archiveBytes)}  {ArchiveName}\n");
        FakeReleaseClient releaseClient = new(
            Results.Success("v0.3.0"),
            checksums,
            archiveFailure: Results.Failure("Could not download 'refedle-v0.3.0-linux-x64.tar.gz' (status: 404 NotFound)."));
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();
        UpdateCommand command = new("0.2.0", releaseClient, replacer, RidStub(), logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

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
        byte[] wrongChecksums = Encoding.UTF8.GetBytes($"{new string('a', 64)}  {ArchiveName}\n");
        FakeReleaseClient releaseClient = new(Results.Success("v0.3.0"), wrongChecksums, _archiveBytes);
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();
        UpdateCommand command = new("0.2.0", releaseClient, replacer, RidStub(), logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().ContainSingle().Which.Should().Contain("Checksum mismatch");
    }

    [Fact]
    public async Task RunAsync_WhenBinaryReplacerFails_ReturnsFailure()
    {
        // Arrange
        byte[] checksums = Encoding.UTF8.GetBytes($"{Sha256Hex(_archiveBytes)}  {ArchiveName}\n");
        FakeReleaseClient releaseClient = new(Results.Success("v0.3.0"), checksums, _archiveBytes);
        FakeBinaryReplacer replacer = new(Results.Failure("The archive does not contain the 'refedle' binary."));
        TestAppLogger logger = new();
        UpdateCommand command = new("0.2.0", releaseClient, replacer, RidStub(), logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

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
        ThrowingReleaseClient releaseClient = new(CreateException(exceptionKind));
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();
        UpdateCommand command = new("0.2.0", releaseClient, replacer, RidStub(), logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().ContainSingle().Which.Should().StartWith(expectedMessagePrefix);
    }

    [Fact]
    public async Task RunAsync_WhenCancellationRequested_ReturnsFailureWithCancelledMessage()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        ThrowingReleaseClient releaseClient = new(new OperationCanceledException(cts.Token));
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();
        UpdateCommand command = new("0.2.0", releaseClient, replacer, RidStub(), logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(cts.Token);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Update cancelled.");
    }

    [Fact]
    public async Task RunAsync_WhenCancelledDuringArchiveDownload_ReturnsFailureAndRemovesDownloadDirectory()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        byte[] checksums = Encoding.UTF8.GetBytes($"{Sha256Hex(_archiveBytes)}  {ArchiveName}\n");
        CancellingArchiveReleaseClient releaseClient = new(Results.Success("v0.3.0"), checksums, cts);
        FakeBinaryReplacer replacer = new(Results.Success());
        TestAppLogger logger = new();
        UpdateCommand command = new("0.2.0", releaseClient, replacer, RidStub(), logger, new TestStatusReporter());

        // Act
        ExitCode exitCode = await command.RunAsync(cts.Token);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
        replacer.CallCount.Should().Be(0);
        logger.Errors.Should().ContainSingle().Which.Should().Be("Update cancelled.");
        releaseClient.DownloadDirectoryExistedWhenArchiveWasRequested.Should().BeTrue();
        Directory.Exists(releaseClient.DownloadDirectoryPath).Should().BeFalse();
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

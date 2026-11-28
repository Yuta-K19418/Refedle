using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Refedle.Engine;

namespace Refedle.App.Cli.Commands.Update;

/// <summary>
/// The <c>refedle update</c> command: checks the latest GitHub release and replaces the
/// running binary when a newer version is available.
/// </summary>
internal sealed class UpdateCommand
{
    private const string DevVersion = "0.0.0-dev";
    private const string ChecksumsFileName = "checksums.txt";
    private const int StreamBufferSize = 8192;

    private readonly string _currentVersion;
    private readonly IReleaseClient _releaseClient;
    private readonly IBinaryReplacer _binaryReplacer;
    private readonly IRuntimeIdentifierResolver _ridResolver;
    private readonly IAppLogger _logger;
    private readonly IStatusReporter _statusReporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCommand"/> class.
    /// </summary>
    /// <param name="currentVersion">The version of the running binary, e.g. <see cref="BuildInfo.Version"/>.</param>
    /// <param name="releaseClient">The client used to resolve the latest release and download assets.</param>
    /// <param name="binaryReplacer">The component that replaces the running binary with the archive content.</param>
    /// <param name="ridResolver">Resolves the release runtime identifier for the running process.</param>
    /// <param name="logger">The logger used for progress and error output.</param>
    /// <param name="statusReporter">Shows the current phase while updating; the indicator shown while downloading and installing.</param>
    public UpdateCommand(
        string currentVersion,
        IReleaseClient releaseClient,
        IBinaryReplacer binaryReplacer,
        IRuntimeIdentifierResolver ridResolver,
        IAppLogger logger,
        IStatusReporter statusReporter)
    {
        ArgumentNullException.ThrowIfNull(statusReporter);

        _currentVersion = currentVersion;
        _releaseClient = releaseClient;
        _binaryReplacer = binaryReplacer;
        _ridResolver = ridResolver;
        _logger = logger;
        _statusReporter = statusReporter;
    }

    /// <summary>
    /// Runs the self-update flow.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The exit code for the process.</returns>
    public async Task<ExitCode> RunAsync(CancellationToken cancellationToken)
    {
        if (_currentVersion == DevVersion)
        {
            await _logger.WriteInfoAsync(
                "This is a development build; 'refedle update' is disabled. Use install.sh to install a released version.")
                .ConfigureAwait(false);
            return ExitCode.Success;
        }

        return await RunGuardedAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ExitCode> RunGuardedAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await RunUpdateFlowAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await FailAsync("Update cancelled.").ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return await FailAsync($"Network error while contacting GitHub: {ex.Message}").ConfigureAwait(false);
        }
        catch (TaskCanceledException ex)
        {
            // A timeout surfaces as TaskCanceledException without the user's token being cancelled.
            return await FailAsync($"Network error while contacting GitHub: {ex.Message}").ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            return await FailAsync($"Permission denied during update: {ex.Message}").ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            return await FailAsync($"File error during update: {ex.Message}").ConfigureAwait(false);
        }
        catch (InvalidDataException ex)
        {
            return await FailAsync($"The downloaded archive is corrupted: {ex.Message}").ConfigureAwait(false);
        }
    }

    private async Task<ExitCode> RunUpdateFlowAsync(CancellationToken cancellationToken)
    {
        if (!ReleaseVersion.TryParse(_currentVersion, out ReleaseVersion current))
        {
            return await FailAsync($"Invalid current version: '{_currentVersion}'.").ConfigureAwait(false);
        }

        Result<string> tagResult = await _releaseClient.GetLatestTagAsync(cancellationToken).ConfigureAwait(false);
        if (tagResult.IsFailure)
        {
            return await FailAsync(tagResult.Error).ConfigureAwait(false);
        }

        if (!ReleaseVersion.TryParse(tagResult.Value, out ReleaseVersion latest))
        {
            return await FailAsync($"Could not parse the latest release tag: '{tagResult.Value}'.").ConfigureAwait(false);
        }

        await _logger.WriteInfoAsync($"Current version: {current}").ConfigureAwait(false);
        await _logger.WriteInfoAsync($"Latest version:  {latest}").ConfigureAwait(false);
        await _logger.WriteInfoAsync(string.Empty).ConfigureAwait(false);

        if (current >= latest)
        {
            await _logger.WriteInfoAsync("Already up to date.").ConfigureAwait(false);
            return ExitCode.Success;
        }

        Result<string> ridResult = _ridResolver.Resolve();
        if (ridResult.IsFailure)
        {
            return await FailAsync(ridResult.Error).ConfigureAwait(false);
        }

        string archiveName = $"refedle-{tagResult.Value}-{ridResult.Value}.tar.gz";
        await _logger.WriteInfoAsync($"Downloading refedle {latest}...").ConfigureAwait(false);

        DirectoryInfo tempDirectory = Directory.CreateTempSubdirectory("refedle-update-");
        try
        {
            Result updateResult = await _statusReporter.RunAsync(
                "Downloading...",
                setPhase => UpdateAsync(
                    tagResult.Value, archiveName, tempDirectory.FullName, setPhase, cancellationToken)).ConfigureAwait(false);
            if (updateResult.IsFailure)
            {
                return await FailAsync(updateResult.Error).ConfigureAwait(false);
            }

            await _logger.WriteInfoAsync($"Updated successfully: {current} -> {latest}").ConfigureAwait(false);
            return ExitCode.Success;
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private async ValueTask<Result> UpdateAsync(
        string tag, string archiveName, string tempDirectoryPath, Action<string> setPhase, CancellationToken cancellationToken)
    {
        string checksumsPath = Path.Combine(tempDirectoryPath, ChecksumsFileName);
        Result checksumsResult = await _releaseClient.DownloadAssetAsync(
            tag, ChecksumsFileName, checksumsPath, cancellationToken).ConfigureAwait(false);
        if (checksumsResult.IsFailure)
        {
            return checksumsResult;
        }

        string archivePath = Path.Combine(tempDirectoryPath, archiveName);
        Result archiveResult = await _releaseClient.DownloadAssetAsync(
            tag, archiveName, archivePath, cancellationToken).ConfigureAwait(false);
        if (archiveResult.IsFailure)
        {
            return archiveResult;
        }

        return await InstallAsync(checksumsPath, archivePath, archiveName, setPhase, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> InstallAsync(
        string checksumsPath, string archivePath, string archiveName, Action<string> setPhase, CancellationToken cancellationToken)
    {
        setPhase("Verifying checksum...");
        Result verifyResult = await VerifyChecksumAsync(checksumsPath, archivePath, archiveName, cancellationToken).ConfigureAwait(false);
        if (verifyResult.IsFailure)
        {
            return verifyResult;
        }

        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
        {
            return Results.Failure("Could not determine the path of the running binary.");
        }

        setPhase("Replacing binary...");
        Result replaceResult = await _binaryReplacer.ReplaceAsync(archivePath, processPath, cancellationToken).ConfigureAwait(false);
        return replaceResult;
    }

    private static async ValueTask<Result> VerifyChecksumAsync(
        string checksumsPath, string archivePath, string archiveName, CancellationToken cancellationToken)
    {
        string content = await File.ReadAllTextAsync(checksumsPath, cancellationToken).ConfigureAwait(false);
        Result<string> expectedHexResult = Checksums.FindHex(content, archiveName);
        if (expectedHexResult.IsFailure)
        {
            return Results.Failure(expectedHexResult.Error);
        }

        FileStream archiveStream = new(
            archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, StreamBufferSize, useAsync: true);
        await using ConfiguredAsyncDisposable archiveStreamDisposer = archiveStream.ConfigureAwait(false);
        string actualHex = Convert.ToHexString(await SHA256.HashDataAsync(archiveStream, cancellationToken).ConfigureAwait(false));
        return actualHex == expectedHexResult.Value
            ? Results.Success()
            : Results.Failure($"Checksum mismatch for '{archiveName}'.");
    }

    private static void TryDeleteDirectory(DirectoryInfo directory)
    {
        try
        {
            directory.Delete(recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup: a leftover temp directory must not mask the real update result.
        }
    }

    private async Task<ExitCode> FailAsync(string message)
    {
        await _logger.WriteErrorAsync(message).ConfigureAwait(false);
        return ExitCode.Failure;
    }
}

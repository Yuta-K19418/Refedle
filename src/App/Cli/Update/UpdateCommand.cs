using System.Security.Cryptography;
using Refedle.Engine;

namespace Refedle.App.Cli.Update;

/// <summary>
/// The <c>refedle update</c> command: checks the latest GitHub release and replaces the
/// running binary when a newer version is available.
/// </summary>
/// <param name="currentVersion">The version of the running binary, e.g. <see cref="BuildInfo.Version"/>.</param>
/// <param name="releaseClient">The client used to resolve the latest release and download assets.</param>
/// <param name="binaryReplacer">The component that replaces the running binary with the archive content.</param>
/// <param name="ridResolver">Resolves the release runtime identifier for the running process.</param>
/// <param name="logger">The logger used for progress and error output.</param>
internal sealed class UpdateCommand(
    string currentVersion,
    IReleaseClient releaseClient,
    IBinaryReplacer binaryReplacer,
    IRuntimeIdentifierResolver ridResolver,
    IAppLogger logger)
{
    private const string DevVersion = "0.0.0-dev";
    private const string ChecksumsFileName = "checksums.txt";
    private const int StreamBufferSize = 8192;

    /// <summary>
    /// Runs the self-update flow.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The exit code for the process.</returns>
    public async Task<ExitCode> RunAsync(CancellationToken cancellationToken)
    {
        if (currentVersion == DevVersion)
        {
            await logger.WriteInfoAsync(
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
        if (!ReleaseVersion.TryParse(currentVersion, out var current))
        {
            return await FailAsync($"Invalid current version: '{currentVersion}'.").ConfigureAwait(false);
        }

        var tagResult = await releaseClient.GetLatestTagAsync(cancellationToken).ConfigureAwait(false);
        if (tagResult.IsFailure)
        {
            return await FailAsync(tagResult.Error).ConfigureAwait(false);
        }

        if (!ReleaseVersion.TryParse(tagResult.Value, out var latest))
        {
            return await FailAsync($"Could not parse the latest release tag: '{tagResult.Value}'.").ConfigureAwait(false);
        }

        await logger.WriteInfoAsync($"Current version: {current}").ConfigureAwait(false);
        await logger.WriteInfoAsync($"Latest version:  {latest}").ConfigureAwait(false);
        await logger.WriteInfoAsync(string.Empty).ConfigureAwait(false);

        if (current >= latest)
        {
            await logger.WriteInfoAsync("Already up to date.").ConfigureAwait(false);
            return ExitCode.Success;
        }

        var ridResult = ridResolver.Resolve();
        if (ridResult.IsFailure)
        {
            return await FailAsync(ridResult.Error).ConfigureAwait(false);
        }

        var archiveName = $"refedle-{tagResult.Value}-{ridResult.Value}.tar.gz";
        await logger.WriteInfoAsync($"Downloading refedle {latest}...").ConfigureAwait(false);

        var tempDirectory = Directory.CreateTempSubdirectory("refedle-update-");
        try
        {
            var checksumsPath = Path.Combine(tempDirectory.FullName, ChecksumsFileName);
            var checksumsResult = await releaseClient.DownloadAssetAsync(
                tagResult.Value, ChecksumsFileName, checksumsPath, cancellationToken).ConfigureAwait(false);
            if (checksumsResult.IsFailure)
            {
                return await FailAsync(checksumsResult.Error).ConfigureAwait(false);
            }

            var archivePath = Path.Combine(tempDirectory.FullName, archiveName);
            var archiveResult = await releaseClient.DownloadAssetAsync(
                tagResult.Value, archiveName, archivePath, cancellationToken).ConfigureAwait(false);
            if (archiveResult.IsFailure)
            {
                return await FailAsync(archiveResult.Error).ConfigureAwait(false);
            }

            var installResult = await InstallAsync(checksumsPath, archivePath, archiveName, cancellationToken).ConfigureAwait(false);
            if (installResult.IsFailure)
            {
                return await FailAsync(installResult.Error).ConfigureAwait(false);
            }

            await logger.WriteInfoAsync($"Updated successfully: {current} -> {latest}").ConfigureAwait(false);
            return ExitCode.Success;
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private async Task<Result> InstallAsync(string checksumsPath, string archivePath, string archiveName, CancellationToken cancellationToken)
    {
        await logger.WriteInfoAsync("Verifying checksum...").ConfigureAwait(false);
        var verifyResult = await VerifyChecksumAsync(checksumsPath, archivePath, archiveName, cancellationToken).ConfigureAwait(false);
        if (verifyResult.IsFailure)
        {
            return verifyResult;
        }

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
        {
            return Results.Failure("Could not determine the path of the running binary.");
        }

        var replaceResult = await binaryReplacer.ReplaceAsync(archivePath, processPath, cancellationToken).ConfigureAwait(false);
        return replaceResult;
    }

    private static async ValueTask<Result> VerifyChecksumAsync(
        string checksumsPath, string archivePath, string archiveName, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(checksumsPath, cancellationToken).ConfigureAwait(false);
        var expectedHexResult = Checksums.FindHex(content, archiveName);
        if (expectedHexResult.IsFailure)
        {
            return Results.Failure(expectedHexResult.Error);
        }

        await using var archiveStream = new FileStream(
            archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, StreamBufferSize, useAsync: true);
        var actualHex = Convert.ToHexString(await SHA256.HashDataAsync(archiveStream, cancellationToken).ConfigureAwait(false));
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
        await logger.WriteErrorAsync(message).ConfigureAwait(false);
        return ExitCode.Failure;
    }
}

using Refedle.App.Cli.Commands.Update;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli.Commands.Update;

/// <summary>
/// <see cref="IReleaseClient"/> that completes the checksums download, then cancels the supplied
/// source and throws when the archive download starts, so update-flow tests can observe what
/// happens to the download directory when a cancellation arrives mid-update.
/// </summary>
internal sealed class CancellingArchiveReleaseClient(
    Result<string> latestTag,
    byte[] checksumsFile,
    CancellationTokenSource cancellationTokenSource) : IReleaseClient
{
    private const string ChecksumsFileName = "checksums.txt";

    public string? DownloadDirectoryPath { get; private set; }

    public bool DownloadDirectoryExistedWhenArchiveWasRequested { get; private set; }

    public ValueTask<Result<string>> GetLatestTagAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(latestTag);

    public async ValueTask<Result> DownloadAssetAsync(
        string tag, string fileName, string destinationPath, CancellationToken cancellationToken)
    {
        DownloadDirectoryPath = Path.GetDirectoryName(destinationPath);
        if (string.Equals(fileName, ChecksumsFileName, StringComparison.Ordinal))
        {
            await File.WriteAllBytesAsync(destinationPath, checksumsFile, cancellationToken).ConfigureAwait(false);
            return Results.Success();
        }

        DownloadDirectoryExistedWhenArchiveWasRequested = Directory.Exists(DownloadDirectoryPath);
        await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
        throw new OperationCanceledException(cancellationTokenSource.Token);
    }
}

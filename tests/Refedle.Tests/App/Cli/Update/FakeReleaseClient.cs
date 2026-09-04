using Refedle.App.Cli.Update;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli.Update;

/// <summary>
/// In-memory <see cref="IReleaseClient"/> for update-flow tests: returns a fixed latest tag,
/// records every asset request, and writes preconfigured asset bytes to disk without any
/// network access. Each asset (the checksums file and the archive) can be made to fail
/// independently to exercise the command's short-circuit behavior.
/// </summary>
internal sealed class FakeReleaseClient(
    Result<string> latestTag,
    byte[]? checksumsFile = null,
    byte[]? archiveFile = null,
    Result? checksumsFailure = null,
    Result? archiveFailure = null) : IReleaseClient
{
    private const string ChecksumsFileName = "checksums.txt";

    private readonly List<AssetDownloadRequest> _downloadRequests = [];

    public int GetLatestTagCallCount { get; private set; }

    public IReadOnlyList<AssetDownloadRequest> DownloadRequests => _downloadRequests;

    public IReadOnlyList<string> DownloadedFileNames => [.. _downloadRequests.Select(request => request.FileName)];

    public ValueTask<Result<string>> GetLatestTagAsync(CancellationToken cancellationToken)
    {
        GetLatestTagCallCount++;
        return ValueTask.FromResult(latestTag);
    }

    public async ValueTask<Result> DownloadAssetAsync(
        string tag, string fileName, string destinationPath, CancellationToken cancellationToken)
    {
        _downloadRequests.Add(new AssetDownloadRequest(tag, fileName, destinationPath));

        var isChecksums = string.Equals(fileName, ChecksumsFileName, StringComparison.Ordinal);
        var configuredFailure = isChecksums ? checksumsFailure : archiveFailure;
        if (configuredFailure is { } failure)
        {
            return failure;
        }

        var content = isChecksums ? checksumsFile : archiveFile;
        if (content is null)
        {
            return Results.Failure($"Could not download '{fileName}' (status: 404 NotFound).");
        }

        await File.WriteAllBytesAsync(destinationPath, content, cancellationToken).ConfigureAwait(false);
        return Results.Success();
    }
}

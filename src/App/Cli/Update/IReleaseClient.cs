using Refedle.Engine;

namespace Refedle.App.Cli.Update;

/// <summary>
/// Provides access to the GitHub releases of the Refedle distribution repository.
/// </summary>
internal interface IReleaseClient
{
    /// <summary>
    /// Resolves the tag name of the latest release.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The latest tag (e.g. <c>v0.3.0</c>) on success, or a failure message.</returns>
    ValueTask<Result<string>> GetLatestTagAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a release asset to the given destination path.
    /// </summary>
    /// <param name="tag">The release tag the asset belongs to.</param>
    /// <param name="fileName">The asset file name, e.g. <c>checksums.txt</c>.</param>
    /// <param name="destinationPath">The local path to write the asset to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result on completion, or a failure message.</returns>
    ValueTask<Result> DownloadAssetAsync(string tag, string fileName, string destinationPath, CancellationToken cancellationToken);
}

namespace Refedle.Tests.App.Cli.Update;

/// <summary>
/// Records a single <c>DownloadAssetAsync</c> call made during an update-flow test.
/// </summary>
/// <param name="Tag">The release tag the asset was requested for.</param>
/// <param name="FileName">The requested asset file name.</param>
/// <param name="DestinationPath">The local path the asset was written to.</param>
internal sealed record AssetDownloadRequest(string Tag, string FileName, string DestinationPath);

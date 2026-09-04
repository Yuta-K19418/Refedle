using Refedle.App.Cli.Update;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli.Update;

/// <summary>
/// <see cref="IReleaseClient"/> whose <see cref="GetLatestTagAsync"/> always throws a
/// preconfigured exception, so update-flow tests can verify that <c>UpdateCommand</c>
/// translates infrastructure exceptions into clean non-zero results.
/// </summary>
internal sealed class ThrowingReleaseClient(Exception exception) : IReleaseClient
{
    public ValueTask<Result<string>> GetLatestTagAsync(CancellationToken cancellationToken)
        => throw exception;

    public ValueTask<Result> DownloadAssetAsync(
        string tag, string fileName, string destinationPath, CancellationToken cancellationToken)
        => throw exception;
}

using Refedle.App.Cli.Update;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli.Update;

/// <summary>
/// Records the arguments of the single expected <see cref="IBinaryReplacer.ReplaceAsync"/> call
/// and returns a preconfigured result instead of touching the file system.
/// </summary>
internal sealed class FakeBinaryReplacer(Result result) : IBinaryReplacer
{
    public int CallCount { get; private set; }

    public string? ReceivedArchivePath { get; private set; }

    public string? ReceivedTargetPath { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public ValueTask<Result> ReplaceAsync(string archiveFilePath, string targetFilePath, CancellationToken cancellationToken)
    {
        CallCount++;
        ReceivedArchivePath = archiveFilePath;
        ReceivedTargetPath = targetFilePath;
        ReceivedCancellationToken = cancellationToken;
        return ValueTask.FromResult(result);
    }
}

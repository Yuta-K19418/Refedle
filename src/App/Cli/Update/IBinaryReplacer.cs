using Refedle.Engine;

namespace Refedle.App.Cli.Update;

/// <summary>
/// Replaces the currently running binary with the refedle binary contained in a release archive.
/// </summary>
internal interface IBinaryReplacer
{
    /// <summary>
    /// Extracts the <c>refedle</c> binary from the given <c>.tar.gz</c> archive and atomically
    /// replaces the target file with it.
    /// </summary>
    /// <param name="archiveFilePath">The path of the downloaded <c>refedle-&lt;tag&gt;-&lt;rid&gt;.tar.gz</c>.</param>
    /// <param name="targetFilePath">The path of the binary to replace.</param>
    /// <param name="cancellationToken">A token to cancel the operation before the irreversible replacement.</param>
    /// <returns>A successful result on completion, or a failure message.</returns>
    ValueTask<Result> ReplaceAsync(string archiveFilePath, string targetFilePath, CancellationToken cancellationToken);
}

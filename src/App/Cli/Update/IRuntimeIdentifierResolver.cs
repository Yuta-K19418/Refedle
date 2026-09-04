using Refedle.Engine;

namespace Refedle.App.Cli.Update;

/// <summary>
/// Resolves the release runtime identifier (RID) for the running process.
/// </summary>
internal interface IRuntimeIdentifierResolver
{
    /// <summary>
    /// Resolves the RID for the running OS and architecture.
    /// </summary>
    /// <returns>The RID (e.g. <c>linux-x64</c>) on success, or a failure explaining why the platform is unsupported.</returns>
    Result<string> Resolve();
}

using System.Runtime.InteropServices;
using Refedle.Engine;

namespace Refedle.App.Cli.Update;

/// <summary>
/// <see cref="IRuntimeIdentifierResolver"/> backed by <see cref="RidMapper"/> and the running OS.
/// </summary>
internal sealed class RuntimeIdentifierResolver : IRuntimeIdentifierResolver
{
    /// <inheritdoc/>
    public Result<string> Resolve()
        => RidMapper.Resolve(RidMapper.DetectPlatform(), RuntimeInformation.OSArchitecture);
}

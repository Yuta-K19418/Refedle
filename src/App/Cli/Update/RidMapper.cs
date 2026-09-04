using System.Runtime.InteropServices;
using Refedle.Engine;

namespace Refedle.App.Cli.Update;

/// <summary>
/// Maps the running OS platform and architecture to the release runtime identifier (RID).
/// </summary>
internal static class RidMapper
{
    private const string RepositoryUrl = "https://github.com/Yuta-K19418/Refedle";

    /// <summary>
    /// Resolves the RID for the given platform and architecture.
    /// </summary>
    /// <param name="platform">The OS platform, e.g. <see cref="OSPlatform.Linux"/>.</param>
    /// <param name="architecture">The OS architecture.</param>
    /// <returns>The RID on success, or a failure explaining why the combination is unsupported.</returns>
    public static Result<string> Resolve(OSPlatform platform, Architecture architecture)
    {
        if (platform == OSPlatform.OSX)
        {
            if (architecture == Architecture.Arm64)
            {
                return Results.Success("osx-arm64");
            }

            return Results.Failure<string>(
                "macOS on Intel (osx-x64) is not supported. Build from source: " + RepositoryUrl);
        }

        if (platform == OSPlatform.Linux)
        {
            if (architecture == Architecture.X64)
            {
                return Results.Success("linux-x64");
            }

            if (architecture == Architecture.Arm64)
            {
                return Results.Success("linux-arm64");
            }
        }

        if (platform == OSPlatform.Windows)
        {
            return Results.Failure<string>(
                "Windows is not supported by 'refedle update'. Download the archive from " + RepositoryUrl + "/releases and replace the binary manually.");
        }

        return Results.Failure<string>($"Unsupported platform/architecture: {platform} {architecture}.");
    }

    /// <summary>
    /// Detects the current OS platform via <see cref="OperatingSystem"/>.
    /// </summary>
    /// <returns>The detected platform, or a synthetic "Unknown" platform.</returns>
    public static OSPlatform DetectPlatform()
    {
        if (OperatingSystem.IsMacOS())
        {
            return OSPlatform.OSX;
        }

        if (OperatingSystem.IsLinux())
        {
            return OSPlatform.Linux;
        }

        if (OperatingSystem.IsWindows())
        {
            return OSPlatform.Windows;
        }

        return OSPlatform.Create("Unknown");
    }
}

namespace Refedle.E2ETests.Helpers;

/// <summary>
/// Resolves the path of the refedle application assembly for the current build configuration.
/// </summary>
internal static class AppPathResolver
{
    /// <summary>
    /// Gets the absolute path of refedle.dll.
    /// The ProjectReference from this test project makes MSBuild build src/App in the same
    /// configuration and copy the assembly next to the test assembly, so the configuration
    /// always matches without any relative-path arithmetic.
    /// </summary>
    public static string AppDllPath { get; } = Path.Combine(AppContext.BaseDirectory, "refedle.dll");
}

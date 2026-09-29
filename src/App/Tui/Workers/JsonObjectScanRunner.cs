using Refedle.Engine.IO.JsonObject;

namespace Refedle.App.Tui.Workers;

/// <summary>
/// Runs <see cref="TopLevelScanner.Scan"/> on a background thread.
/// </summary>
internal static class JsonObjectScanRunner
{
    /// <summary>
    /// Runs the JSON Object top-level scan for <paramref name="filePath"/> on a background thread.
    /// </summary>
    /// <param name="filePath">The file to scan.</param>
    /// <param name="ct">The cancellation token to observe.</param>
    /// <returns>The scanned top-level entries once the scan completes.</returns>
    public static Task<IReadOnlyList<JsonObjectEntry>> RunAsync(string filePath, CancellationToken ct)
    {
        return Task.Run(() => TopLevelScanner.Scan(filePath, ct), ct);
    }
}

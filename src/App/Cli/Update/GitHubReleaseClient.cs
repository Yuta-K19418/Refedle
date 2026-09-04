using System.Globalization;
using System.Text.RegularExpressions;
using Refedle.Engine;

namespace Refedle.App.Cli.Update;

/// <summary>
/// <see cref="IReleaseClient"/> implementation backed by the GitHub releases of
/// <c>Yuta-K19418/Refedle</c>.
/// </summary>
internal sealed partial class GitHubReleaseClient : IReleaseClient, IDisposable
{
    private const string BaseUrl = "https://github.com/Yuta-K19418/Refedle";

    private static readonly Uri LatestReleaseUri = new($"{BaseUrl}/releases/latest");

    // Auto-redirect must stay off so the /releases/latest redirect itself is observable.
    private readonly SocketsHttpHandler _latestHandler = new() { AllowAutoRedirect = false };

    private readonly HttpClient _latestClient;

    private readonly HttpClient _downloadClient = new();

    public GitHubReleaseClient()
    {
        _latestClient = new HttpClient(_latestHandler);
    }

    /// <summary>
    /// Extracts the release tag from a <c>releases/latest</c> redirect target.
    /// </summary>
    /// <param name="location">The <c>Location</c> header value, absolute or relative.</param>
    /// <param name="tag">The extracted tag (e.g. <c>v0.3.0</c>) when successful.</param>
    /// <returns><c>true</c> when a tag could be extracted; otherwise <c>false</c>.</returns>
    public static bool TryExtractTag(string location, out string tag)
    {
        var match = TagRegex().Match(location);
        if (match.Success)
        {
            tag = match.Groups["tag"].Value;
            return true;
        }

        tag = string.Empty;
        return false;
    }

    /// <inheritdoc/>
    public async ValueTask<Result<string>> GetLatestTagAsync(CancellationToken cancellationToken)
    {
        using var response = await _latestClient.GetAsync(LatestReleaseUri, cancellationToken)
            .ConfigureAwait(false);

        var location = response.Headers.Location?.ToString();
        if ((int)response.StatusCode is >= 300 and < 400
            && location is not null
            && TryExtractTag(location, out var tag))
        {
            return Results.Success(tag);
        }

        var message = string.Create(
            CultureInfo.InvariantCulture, $"Could not resolve the latest release tag (status: {(int)response.StatusCode}).");
        return Results.Failure<string>(message);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> DownloadAssetAsync(string tag, string fileName, string destinationPath, CancellationToken cancellationToken)
    {
        var url = new Uri($"{BaseUrl}/releases/download/{tag}/{fileName}");
        using var response = await _downloadClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var message = string.Create(
                CultureInfo.InvariantCulture, $"Could not download '{fileName}' (status: {(int)response.StatusCode} {response.StatusCode}).");
            return Results.Failure(message);
        }

        var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            await using var destination = new FileStream(
                destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true);
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        return Results.Success();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _latestClient.Dispose();
        _latestHandler.Dispose();
        _downloadClient.Dispose();
    }

    [GeneratedRegex("releases/tag/(?<tag>[^/]+)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex TagRegex();
}

using System.Text.RegularExpressions;
using Refedle.Engine;

namespace Refedle.App.Cli.Update;

/// <summary>
/// Parses <c>checksums.txt</c> content in the <c>sha256sum</c> compatible format
/// (<c>&lt;hex&gt;  &lt;filename&gt;</c> per line) and looks up entries by file name.
/// </summary>
internal static partial class Checksums
{
    /// <summary>
    /// Extracts the SHA-256 hex digest recorded for the given file.
    /// </summary>
    /// <param name="content">The full <c>checksums.txt</c> content.</param>
    /// <param name="fileName">The archive file name to look up.</param>
    /// <returns>The lower-case hex digest on success, or a failure describing the problem.</returns>
    public static Result<string> FindHex(string content, string fileName)
    {
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var match = ChecksumLineRegex().Match(line);
            if (!match.Success)
            {
                return Results.Failure<string>($"Invalid checksums line: '{line}'");
            }

            if (match.Groups["name"].Value == fileName)
            {
                return Results.Success(match.Groups["hex"].Value.ToUpperInvariant());
            }
        }

        return Results.Failure<string>($"No checksum entry for '{fileName}' in checksums.txt.");
    }

    [GeneratedRegex(
        "^(?<hex>[0-9a-fA-F]{64})[ \\t]+\\*?(?<name>\\S+)$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex ChecksumLineRegex();
}

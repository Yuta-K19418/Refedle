using System.Globalization;
using System.Runtime.InteropServices;

namespace Refedle.App.Cli.Update;

/// <summary>
/// A parsed semantic version of the <c>Major.Minor.Patch</c> form used by Refedle release tags.
/// </summary>
/// <param name="Major">The major version component.</param>
/// <param name="Minor">The minor version component.</param>
/// <param name="Patch">The patch version component.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ReleaseVersion(int Major, int Minor, int Patch) : IComparable<ReleaseVersion>
{
    /// <summary>
    /// Attempts to parse <c>Major.Minor.Patch</c> with an optional leading <c>v</c>.
    /// </summary>
    /// <param name="text">The version text, e.g. <c>0.3.0</c> or <c>v0.3.0</c>.</param>
    /// <param name="version">The parsed version when successful.</param>
    /// <returns><c>true</c> when the text is a valid version; otherwise <c>false</c>.</returns>
    public static bool TryParse(string text, out ReleaseVersion version)
    {
        var body = text.StartsWith('v') ? text[1..] : text;
        var parts = body.Split('.');
        if (parts.Length != 3)
        {
            version = default;
            return false;
        }

        // None disallows signs and whitespace so that only plain digits are accepted.
        const NumberStyles Digits = NumberStyles.None;
        if (!int.TryParse(parts[0], Digits, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], Digits, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(parts[2], Digits, CultureInfo.InvariantCulture, out var patch))
        {
            version = default;
            return false;
        }

        version = new ReleaseVersion(major, minor, patch);
        return true;
    }

    /// <inheritdoc/>
    public int CompareTo(ReleaseVersion other)
    {
        if (Major != other.Major)
        {
            return Major.CompareTo(other.Major);
        }

        if (Minor != other.Minor)
        {
            return Minor.CompareTo(other.Minor);
        }

        return Patch.CompareTo(other.Patch);
    }

    /// <inheritdoc/>
    public override string ToString() => $"v{Major}.{Minor}.{Patch}";

    public static bool operator <(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) < 0;

    public static bool operator <=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) > 0;

    public static bool operator >=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) >= 0;
}

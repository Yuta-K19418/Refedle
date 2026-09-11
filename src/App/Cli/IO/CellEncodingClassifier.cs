using System.Globalization;

namespace Refedle.App.Cli.IO;

/// <summary>
///  Shared text-to-<see cref="CellEncoding"/> heuristic used by CSV reads and by
///  transformed-column output. Mirrors the historical <c>WriteJsonValue</c>
///  detection order (bool → long → double → text) so CSV→JSON Lines and
///  transformed-column output stay byte-for-byte unchanged.
/// </summary>
internal static class CellEncodingClassifier
{
    /// <summary>
    ///  Classifies plain cell text into the encoding the JSON Lines writer needs.
    /// </summary>
    public static CellEncoding Classify(ReadOnlySpan<char> value)
    {
        if (bool.TryParse(value, out _))
        {
            return CellEncoding.Boolean;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return CellEncoding.Numeric;
        }

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            return CellEncoding.Numeric;
        }

        return CellEncoding.PlainText;
    }
}

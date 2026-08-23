using System.Text;

namespace Refedle.Engine.Utilities;

/// <summary>
/// Stateless string and span helpers shared across the engine layer.
/// </summary>
public static class StringUtility
{
    /// <summary>Returns <c>true</c> if every byte is ASCII whitespace (space, tab, CR, or LF).</summary>
    public static bool IsWhiteSpace(ReadOnlySpan<byte> span)
    {
        foreach (var b in span)
        {
            if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\r' && b != (byte)'\n')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Wraps <paramref name="value"/> in double quotes, escaping <c>\</c> and <c>"</c>.</summary>
    public static string QuoteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    /// <summary>
    /// Strips a surrounding pair of double quotes and unescapes <c>\"</c>/<c>\\</c>.
    /// Any other backslash sequence is left untouched, so no data is lost.
    /// Returns <paramref name="value"/> unchanged if it is not quoted.
    /// </summary>
    public static string UnquoteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return value;
        }

        var inner = value.AsSpan(1, value.Length - 2);
        return inner.IndexOf('\\') < 0 ? inner.ToString() : UnescapeString(inner);
    }

    private static string UnescapeString(ReadOnlySpan<char> inner)
    {
        var sb = new StringBuilder(inner.Length);
        var i = 0;
        while (i < inner.Length)
        {
            if (inner[i] == '\\' && i + 1 < inner.Length && (inner[i + 1] == '"' || inner[i + 1] == '\\'))
            {
                sb.Append(inner[i + 1]);
                i += 2;
                continue;
            }

            sb.Append(inner[i]);
            i++;
        }

        return sb.ToString();
    }
}

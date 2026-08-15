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
}

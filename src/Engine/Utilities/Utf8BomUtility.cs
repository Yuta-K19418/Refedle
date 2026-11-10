namespace Refedle.Engine.Utilities;

/// <summary>
/// UTF-8 byte order mark (BOM) helpers shared across the engine and app layers.
/// </summary>
public static class Utf8BomUtility
{
    private const byte Byte0 = 0xEF;
    private const byte Byte1 = 0xBB;
    private const byte Byte2 = 0xBF;

    /// <summary>The byte length of a UTF-8 BOM.</summary>
    internal const int Length = 3;

    /// <summary>Returns <c>true</c> if <paramref name="bytes"/> begins with a UTF-8 BOM (EF BB BF).</summary>
    internal static bool StartsWithUtf8Bom(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= Length && bytes[0] == Byte0 && bytes[1] == Byte1 && bytes[2] == Byte2;

    /// <summary>
    /// Returns <paramref name="bytes"/> without a leading UTF-8 BOM when one is present —
    /// the in-memory counterpart of <see cref="SkipUtf8Bom"/> for whole-file byte arrays.
    /// </summary>
    /// <param name="bytes">Bytes that may begin with a UTF-8 BOM (e.g. a file read into memory).</param>
    public static ReadOnlyMemory<byte> TrimLeadingUtf8Bom(ReadOnlyMemory<byte> bytes) =>
        StartsWithUtf8Bom(bytes.Span) ? bytes[Length..] : bytes;

    /// <summary>
    /// Advances <paramref name="stream"/> past a UTF-8 BOM at its current position, or leaves the
    /// position unchanged when no BOM is present (including streams shorter than the BOM).
    /// </summary>
    /// <param name="stream">A seekable stream.</param>
    public static void SkipUtf8Bom(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var startPosition = stream.Position;
        Span<byte> header = stackalloc byte[Length];
        var totalRead = stream.ReadAtLeast(header, Length, throwOnEndOfStream: false);

        if (!StartsWithUtf8Bom(header[..totalRead]))
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
        }
    }
}

using Microsoft.Win32.SafeHandles;
using Refedle.Engine.Utilities;

namespace Refedle.Engine.IO;

/// <summary>
/// Extension methods for <see cref="SafeFileHandle"/> random-access file reads.
/// </summary>
internal static class SafeFileHandleExtensions
{
    /// <summary>
    /// Returns the offset at which the file body starts: the UTF-8 BOM length when the file
    /// begins with a BOM, otherwise <c>0</c> (including files shorter than the BOM).
    /// </summary>
    /// <param name="handle">A readable file handle; the header bytes are read from offset 0.</param>
    public static long GetOffsetAfterUtf8Bom(this SafeFileHandle handle)
    {
        Span<byte> header = stackalloc byte[Utf8BomUtility.Length];
        var headerRead = RandomAccess.Read(handle, header, 0);
        return Utf8BomUtility.StartsWithUtf8Bom(header[..headerRead]) ? Utf8BomUtility.Length : 0L;
    }
}

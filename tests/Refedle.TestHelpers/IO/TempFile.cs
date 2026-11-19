namespace Refedle.TestHelpers.IO;

/// <summary>
/// A uniquely named file in the temp directory that is deleted when disposed. Each instance gets
/// its own Guid-based name, so tests running in parallel never share a path.
/// </summary>
internal sealed class TempFile : IDisposable
{
    private TempFile(string path) => Path = path;

    /// <summary>Gets the full path of the file.</summary>
    public string Path { get; }

    /// <summary>Reserves a unique path without creating the file.</summary>
    public static TempFile ReservePath(string extension) =>
        new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}"));

    /// <summary>Reserves a unique path and creates an empty file there.</summary>
    public static TempFile CreateEmpty(string extension)
    {
        TempFile tempFile = ReservePath(extension);
        using FileStream _ = new(tempFile.Path, FileMode.CreateNew);
        return tempFile;
    }

    public void Dispose()
    {
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }
    }
}

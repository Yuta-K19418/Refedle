using System.Diagnostics.CodeAnalysis;

namespace Refedle.TestHelpers.IO;

/// <summary>
/// Creates several uniquely named temp files and deletes all of them when disposed.
/// </summary>
internal sealed class TempFileCollection(string defaultExtension = "") : IDisposable
{
    private readonly List<TempFile> _files = [];

    /// <summary>Creates a file with the default extension holding <paramref name="content"/>.</summary>
    public string Create(string content) => Create(defaultExtension, content);

    /// <summary>Creates a file with the given extension holding <paramref name="content"/>.</summary>
    public string Create(string extension, string content)
    {
        string path = ReservePath(extension);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Reserves a unique path (cleaned up on dispose) without creating the file.</summary>
    public string ReservePath(string extension)
    {
        TempFile tempFile = TempFile.ReservePath(extension);
        _files.Add(tempFile);
        return tempFile.Path;
    }

    /// <summary>
    /// Deletes every file even when some deletions fail, then reports all failures together.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Every deletion must be attempted; the failures are rethrown together as an AggregateException.")]
    public void Dispose()
    {
        List<Exception>? failures = null;
        foreach (TempFile file in _files)
        {
            try
            {
                file.Dispose();
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(failures);
        }
    }
}

using System.Formats.Tar;
using System.IO.Compression;
using Refedle.Engine;

namespace Refedle.App.Cli.Update;

/// <summary>
/// <see cref="IBinaryReplacer"/> that extracts the <c>refedle</c> binary from a
/// <c>.tar.gz</c> release archive and atomically replaces the target file.
/// </summary>
internal sealed class ArchiveBinaryReplacer : IBinaryReplacer
{
    private const string BinaryEntryName = "refedle";
    private const int StreamBufferSize = 8192;

    /// <inheritdoc/>
    public async ValueTask<Result> ReplaceAsync(string archiveFilePath, string targetFilePath, CancellationToken cancellationToken)
    {
        var targetDirectory = Path.GetDirectoryName(Path.GetFullPath(targetFilePath));
        if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
        {
            return Results.Failure($"The target directory does not exist: '{targetDirectory}'.");
        }

        // The temp file lives in the target directory so the final rename stays on one file system.
        var tempPath = Path.Combine(targetDirectory, $".{Path.GetFileName(targetFilePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var extractResult = await ExtractBinaryAsync(archiveFilePath, tempPath, cancellationToken).ConfigureAwait(false);
            if (extractResult.IsFailure)
            {
                return Results.Failure(extractResult.Error);
            }

            SetExecutableMode(tempPath);

            // The rename below overwrites the live binary; there is no way back once it runs.
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, targetFilePath, overwrite: true);

            return Results.Success();
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static async ValueTask<Result> ExtractBinaryAsync(string archiveFilePath, string tempPath, CancellationToken cancellationToken)
    {
        try
        {
            return await ExtractBinaryCoreAsync(archiveFilePath, tempPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
        {
            // Malformed gzip surfaces as InvalidDataException, a truncated tar as EndOfStreamException;
            // both mean a corrupt archive. Other IOExceptions (disk full, etc.) propagate to the caller.
            return Results.Failure($"The downloaded archive is corrupted: {ex.Message}");
        }
    }

    private static async ValueTask<Result> ExtractBinaryCoreAsync(string archiveFilePath, string tempPath, CancellationToken cancellationToken)
    {
        await using var archiveStream = new FileStream(
            archiveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, StreamBufferSize, useAsync: true);
        await using var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress);
        await using var tarReader = new TarReader(gzipStream);

        TarEntry? entry;
        while ((entry = await tarReader.GetNextEntryAsync(cancellationToken: cancellationToken).ConfigureAwait(false)) is not null)
        {
            if (entry.EntryType is not TarEntryType.RegularFile
                || Path.GetFileName(entry.Name) is not BinaryEntryName)
            {
                continue;
            }

            if (entry.DataStream is null)
            {
                return Results.Failure("The 'refedle' entry in the archive has no content.");
            }

            await using var destination = new FileStream(
                tempPath, FileMode.Create, FileAccess.Write, FileShare.None, StreamBufferSize, useAsync: true);
            await entry.DataStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            return Results.Success();
        }

        return Results.Failure("The archive does not contain the 'refedle' binary.");
    }

    private static void SetExecutableMode(string path)
    {
        // rwxr-xr-x; file modes are not applicable on Windows.
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const UnixFileMode Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        File.SetUnixFileMode(path, Mode);
    }
}

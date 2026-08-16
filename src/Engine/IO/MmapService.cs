using System.Buffers;
using System.Globalization;
using System.IO.MemoryMappedFiles;

namespace Refedle.Engine.IO;

/// <summary>
/// Provides memory-mapped file access for efficient data reading.
/// </summary>
/// <remarks>
/// This service uses <see cref="MemoryMappedFile"/> to enable efficient random access
/// to large files without loading the entire file into memory.
/// Uses <see cref="ArrayPool{T}"/> for temporary buffer management to reduce allocations.
/// </remarks>
public sealed class MmapService : IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly long _length;
    private bool _disposed;

    /// <summary>
    /// Gets the total length of the memory-mapped file in bytes.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The service has been disposed.</exception>
    public long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _length;
        }
    }

    private MmapService(MemoryMappedFile mmf, MemoryMappedViewAccessor accessor, long length)
    {
        _mmf = mmf;
        _accessor = accessor;
        _length = length;
    }

    /// <summary>
    /// Opens a file for memory-mapped access.
    /// </summary>
    /// <param name="filePath">The path to the file to open.</param>
    /// <param name="access">The access mode (default: Read).</param>
    /// <returns>
    /// A Result containing the MmapService on success, or an error message on failure.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null, empty, or whitespace.</exception>
    public static Result<MmapService> Open(string filePath, FileAccess access = FileAccess.Read)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return Results.Failure<MmapService>($"File not found: {filePath}");
            }

            if (fileInfo.Length == 0)
            {
                return Results.Failure<MmapService>($"File is empty: {filePath}");
            }

            var mapProtection = access switch
            {
                FileAccess.Read => MemoryMappedFileAccess.Read,
                FileAccess.ReadWrite => MemoryMappedFileAccess.ReadWrite,
                FileAccess.Write => MemoryMappedFileAccess.Write,
                _ => throw new ArgumentException($"Unsupported FileAccess: {access}", nameof(access)),
            };

            using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                access,
                FileShare.Read,
                bufferSize: 1,
                FileOptions.RandomAccess);

            MemoryMappedFile? mmf = null;
            MemoryMappedViewAccessor? accessor = null;

            try
            {
                mmf = MemoryMappedFile.CreateFromFile(
                    fileStream,
                    mapName: null,
                    capacity: 0,
                    mapProtection,
                    HandleInheritability.None,
                    leaveOpen: false);

                accessor = mmf.CreateViewAccessor(
                    offset: 0,
                    size: 0,
                    mapProtection);

                return Results.Success(new MmapService(mmf, accessor, fileInfo.Length));
            }
            catch
            {
                accessor?.Dispose();
                mmf?.Dispose();
                throw;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Failure<MmapService>($"Access denied: {ex.Message}");
        }
        catch (IOException ex)
        {
            return Results.Failure<MmapService>($"I/O error: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads data from the memory-mapped file into the provided destination span.
    /// </summary>
    /// <param name="offset">The byte offset from the start of the file.</param>
    /// <param name="destination">The span to write the data into.</param>
    /// <exception cref="ObjectDisposedException">The service has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The offset is negative, or the range exceeds the file bounds.
    /// </exception>
    /// <exception cref="IOException">Failed to read the expected number of bytes.</exception>
    public void Read(long offset, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        if (destination.Length == 0)
        {
            return;
        }

        // Check for overflow-safe range validation
        if (offset > _length - destination.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(destination),
                string.Create(CultureInfo.InvariantCulture, $"Range [{offset}, {offset + destination.Length}) exceeds file length {_length}"));
        }

        var buffer = ArrayPool<byte>.Shared.Rent(destination.Length);
        try
        {
            var bytesRead = _accessor.ReadArray(offset, buffer, 0, destination.Length);
            if (bytesRead != destination.Length)
            {
                throw new IOException(string.Create(CultureInfo.InvariantCulture, $"Expected to read {destination.Length} bytes but read {bytesRead}"));
            }

            buffer.AsSpan(0, destination.Length).CopyTo(destination);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Attempts to read data from the memory-mapped file into the provided destination span.
    /// Returns a tuple indicating success or failure with an optional error message.
    /// </summary>
    /// <param name="offset">The byte offset from the start of the file.</param>
    /// <param name="destination">The span to write the data into.</param>
    /// <returns>
    /// A tuple where Success is true if the data was successfully read, and Error contains
    /// the error message if the operation failed, or empty if successful.
    /// </returns>
    public (bool success, string error) TryRead(long offset, Span<byte> destination)
    {
        if (_disposed)
        {
            return (false, "Service has been disposed");
        }

        if (offset < 0)
        {
            return (false, string.Create(CultureInfo.InvariantCulture, $"Offset must be non-negative: {offset}"));
        }

        // Check for overflow-safe range validation
        if (destination.Length > 0 && offset > _length - destination.Length)
        {
            return (false, string.Create(CultureInfo.InvariantCulture, $"Range [{offset}, {offset + destination.Length}) exceeds file length {_length}"));
        }

        try
        {
            Read(offset, destination);
            return (true, string.Empty);
        }
        catch (IOException ex)
        {
            return (false, $"Read failed: {ex.Message}");
        }
        catch (ObjectDisposedException ex)
        {
            return (false, $"Service disposed: {ex.Message}");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return (false, $"Invalid range: {ex.Message}");
        }
    }

    /// <summary>
    /// Releases the memory-mapped file resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _accessor.Dispose();
        _mmf.Dispose();
        _disposed = true;
    }
}

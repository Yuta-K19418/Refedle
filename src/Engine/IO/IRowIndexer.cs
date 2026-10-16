namespace Refedle.Engine.IO;

/// <summary>
/// Provides an interface for indexing rows in a file for random access.
/// </summary>
public interface IRowIndexer
{
    /// <summary>
    /// Builds the row index by scanning the entire file.
    /// </summary>
    void BuildIndex(CancellationToken ct = default);

    /// <summary>
    /// Raised once when the first checkpoint has been indexed.
    /// </summary>
    event Action? FirstCheckpointReached;

    /// <summary>
    /// Raised as indexing progresses. Notifications are throttled by progress rather than raised on every checkpoint.
    /// </summary>
    event Action<long, long>? ProgressChanged;

    /// <summary>
    /// Raised once when BuildIndex returns.
    /// </summary>
    event Action? BuildIndexCompleted;

    /// <summary>
    /// Gets the bytes read so far.
    /// </summary>
    long BytesRead { get; }

    /// <summary>
    /// Gets the total file size in bytes.
    /// </summary>
    long FileSize { get; }

    /// <summary>
    /// Gets the total number of rows indexed.
    /// </summary>
    long TotalRows { get; }

    /// <summary>
    /// Gets whether <see cref="BuildIndex"/> has completed.
    /// </summary>
    bool IsIndexingCompleted { get; }

    /// <summary>
    /// Gets the path to the file being indexed.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets the nearest checkpoint for random access to a target row.
    /// </summary>
    (long byteOffset, int rowOffset) GetCheckPoint(long targetRow);
}

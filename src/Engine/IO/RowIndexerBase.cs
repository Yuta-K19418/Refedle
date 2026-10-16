namespace Refedle.Engine.IO;

/// <summary>
/// Base class for row indexers providing common event handling logic.
/// </summary>
/// <remarks>
/// <para><b>Event Design: Action vs EventHandler</b></para>
/// <para>This class uses Action delegates for events instead of the .NET-recommended EventHandler pattern.
/// This is an intentional design decision justified below:</para>
///
/// <para><b>Action<c>&lt;T&gt;</c> / Action Pros</b></para>
/// <list type="bullet">
///   <item>Simple: no unnecessary 'this' (sender) argument</item>
///   <item>Zero-allocation: no EventArgs instance to allocate</item>
/// </list>
///
/// <para><b>Action<c>&lt;T&gt;</c> / Action Cons</b></para>
/// <list type="bullet">
///   <item>Non-standard: .NET recommends EventHandler<c>&lt;T&gt;</c></item>
///   <item>CA1003 warning: requires suppression</item>
///   <item>No sender info (if needed in future, signature must change)</item>
/// </list>
///
/// <para><b>EventHandler<c>&lt;T&gt;</c> Pros</b></para>
/// <list type="bullet">
///   <item>.NET standard pattern</item>
///   <item>Provides sender (this) automatically</item>
///   <item>Extensible via EventArgs without breaking change</item>
///   <item>No suppression needed</item>
/// </list>
///
/// <para><b>EventHandler<c>&lt;T&gt;</c> Cons</b></para>
/// <list type="bullet">
///   <item>Extra 'this' argument even when unused</item>
///   <item>EventArgs allocation (performance cost in hot path)</item>
/// </list>
///
/// <para><b>Why Action Here?</b></para>
/// <list type="bullet">
///   <item>Internal callback only (not a public API)</item>
///   <item>Hot path: invoked frequently during indexing</item>
///   <item>Sender not needed (notification is the only concern)</item>
///   <item>Simplicity and zero-allocation preferred</item>
/// </list>
/// </remarks>
public abstract class RowIndexerBase : IRowIndexer
{
    private const long NotificationStepDivisor = 100;

    /// <summary>
    /// Tracks if the first checkpoint has been reached.
    /// </summary>
    private bool _firstCheckpointReached;

    private volatile bool _isIndexingCompleted;

    private long _lastNotifiedBytesRead;

    /// <inheritdoc />
    public long FileSize { get; protected set; }

    /// <inheritdoc />
    public bool IsIndexingCompleted => _isIndexingCompleted;

    /// <inheritdoc />
    public abstract long BytesRead { get; }

    /// <inheritdoc />
    public abstract long TotalRows { get; }

    /// <inheritdoc />
    public abstract string FilePath { get; }

    /// <inheritdoc />
    public event Action? FirstCheckpointReached;

    /// <inheritdoc />
    public event Action<long, long>? ProgressChanged;

    /// <inheritdoc />
    public event Action? BuildIndexCompleted;

    /// <inheritdoc />
    public abstract void BuildIndex(CancellationToken ct = default);

    /// <inheritdoc />
    public abstract (long byteOffset, int rowOffset) GetCheckPoint(long targetRow);

    /// <summary>
    /// Helper method to invoke the FirstCheckpointReached event exactly once.
    /// </summary>
    protected void OnFirstCheckpointReached()
    {
        if (!_firstCheckpointReached)
        {
            _firstCheckpointReached = true;
            FirstCheckpointReached?.Invoke();
        }
    }

    /// <summary>
    /// Helper method to invoke the ProgressChanged event, throttled by progress.
    /// </summary>
    /// <remarks>
    /// The event is raised only once the bytes read have advanced by a fixed fraction of the file size
    /// since the last notification, so the subscriber is not flooded on very large files.
    /// </remarks>
    /// <param name="bytesRead">Bytes read so far.</param>
    /// <param name="fileSize">Total file size.</param>
    protected void OnProgressChanged(long bytesRead, long fileSize)
    {
        var lastNotifiedBytesRead = Interlocked.Read(ref _lastNotifiedBytesRead);
        var notificationStep = fileSize / NotificationStepDivisor;
        if (bytesRead - lastNotifiedBytesRead < notificationStep)
        {
            return;
        }

        Interlocked.Exchange(ref _lastNotifiedBytesRead, bytesRead);
        ProgressChanged?.Invoke(bytesRead, fileSize);
    }

    /// <summary>
    /// Helper method to invoke the BuildIndexCompleted event.
    /// </summary>
    protected void OnBuildIndexCompleted()
    {
        _isIndexingCompleted = true;
        BuildIndexCompleted?.Invoke();
    }
}

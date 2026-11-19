using Refedle.Engine.IO;

namespace Refedle.TestHelpers.App.Tui.UI;

/// <summary>
/// Mock IRowIndexer for testing.
/// </summary>
internal sealed class CompletedStubRowIndexer(string filePath, long totalRows = 10) : IRowIndexer
{
    public string FilePath => filePath;
    public long FileSize => 1000;
    public long BytesRead => 1000;
    public long TotalRows => totalRows;
    public bool IsIndexingCompleted => true;

#pragma warning disable CS0067
    public event Action? FirstCheckpointReached;
    public event Action<long, long>? ProgressChanged;
    public event Action? BuildIndexCompleted;
#pragma warning restore CS0067

    public void BuildIndex(CancellationToken cancellationToken = default) { }

    public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => (0, 0);
}

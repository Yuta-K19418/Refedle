using System.Text;
using AwesomeAssertions;
using Refedle.App.Cli.IO.Json;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.App.Cli.IO.Json;

public sealed class JsonLinesBatchSourceReaderTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (string path in _tempFiles)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ReadBatch_ReturnsRawLineBytesFromTheCheckpoint()
    {
        // Arrange
        string path = CreateFile("{\"id\":1}\n{\"id\":2}\n{\"id\":3}\n");
        RowIndexer indexer = new(path);
        indexer.BuildIndex(CancellationToken.None);
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(0);
        using JsonLinesBatchSourceReader source = new(new RowReader(path));

        // Act
        IReadOnlyList<JsonRawBytes> batch = source.ReadBatch(byteOffset, rowOffset, 3);

        // Assert
        batch.Select(b => Encoding.UTF8.GetString(b.Span))
            .Should().Equal(["""{"id":1}""", """{"id":2}""", """{"id":3}"""]);
    }

    [Fact]
    public void ReadBatch_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange — disposal delegates to the wrapped RowReader's own guard.
        string path = CreateFile("{\"id\":1}\n");
        RowIndexer indexer = new(path);
        indexer.BuildIndex(CancellationToken.None);
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(0);
        JsonLinesBatchSourceReader source = new(new RowReader(path));
        source.ReadBatch(byteOffset, rowOffset, 1);
        source.Dispose();

        // Act
        Action act = () => source.ReadBatch(byteOffset, rowOffset, 1);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    private string CreateFile(string content)
    {
        string path = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }
}

using System.Text;
using AwesomeAssertions;
using Refedle.App.Cli.IO.Json;
using Refedle.Engine.IO.JsonArray;

namespace Refedle.Tests.App.Cli.IO.Json;

public sealed class JsonArrayBatchSourceReaderTests : IDisposable
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
    public void ReadBatch_ReturnsRawElementBytesFromTheCheckpoint()
    {
        // Arrange
        string path = CreateFile("""[{"id":1},{"id":2},{"id":3}]""");
        RowIndexer indexer = new(path);
        indexer.BuildIndex(CancellationToken.None);
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(0);
        using JsonArrayBatchSourceReader source = new(new ElementReader(path));

        // Act
        IReadOnlyList<JsonRawBytes> batch = source.ReadBatch(byteOffset, rowOffset, 3);

        // Assert
        batch.Select(b => Encoding.UTF8.GetString(b.Span))
            .Should().Equal(["""{"id":1}""", """{"id":2}""", """{"id":3}"""]);
    }

    [Fact]
    public void ReadBatch_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange — disposal delegates to the wrapped ElementReader's own guard.
        string path = CreateFile("""[{"id":1}]""");
        RowIndexer indexer = new(path);
        indexer.BuildIndex(CancellationToken.None);
        (long byteOffset, int rowOffset) = indexer.GetCheckPoint(0);
        JsonArrayBatchSourceReader source = new(new ElementReader(path));
        source.ReadBatch(byteOffset, rowOffset, 1);
        source.Dispose();

        // Act
        Action act = () => source.ReadBatch(byteOffset, rowOffset, 1);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    private string CreateFile(string content)
    {
        string path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }
}

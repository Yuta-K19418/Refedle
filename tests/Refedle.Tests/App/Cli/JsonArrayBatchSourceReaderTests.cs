using System.Text;
using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.Engine.IO.JsonArray;

namespace Refedle.Tests.App.Cli;

public sealed class JsonArrayBatchSourceReaderTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var path in _tempFiles)
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
        var path = CreateFile("""[{"id":1},{"id":2},{"id":3}]""");
        var indexer = new RowIndexer(path);
        indexer.BuildIndex(CancellationToken.None);
        var (byteOffset, rowOffset) = indexer.GetCheckPoint(0);
        using var source = new JsonArrayBatchSourceReader(new ElementReader(path));

        // Act
        var batch = source.ReadBatch(byteOffset, rowOffset, 3);

        // Assert
        batch.Select(b => Encoding.UTF8.GetString(b.Span))
            .Should().Equal(["""{"id":1}""", """{"id":2}""", """{"id":3}"""]);
    }

    [Fact]
    public void ReadBatch_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange — disposal delegates to the wrapped ElementReader's own guard.
        var path = CreateFile("""[{"id":1}]""");
        var indexer = new RowIndexer(path);
        indexer.BuildIndex(CancellationToken.None);
        var (byteOffset, rowOffset) = indexer.GetCheckPoint(0);
        var source = new JsonArrayBatchSourceReader(new ElementReader(path));
        source.ReadBatch(byteOffset, rowOffset, 1);
        source.Dispose();

        // Act
        Action act = () => source.ReadBatch(byteOffset, rowOffset, 1);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    private string CreateFile(string content)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }
}

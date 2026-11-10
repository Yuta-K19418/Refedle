using System.Text;
using AwesomeAssertions;
using Refedle.Engine.IO.JsonArray;

namespace Refedle.Tests.Engine.IO.JsonArray;

public sealed class ElementReaderTests : IDisposable
{
    private readonly string _testFilePath;
    private bool _disposed;

    public ElementReaderTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonarray_elementreader_{Guid.NewGuid()}.json"
        );
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }

            _disposed = true;
        }
    }

    private void WriteTestContent(string content, Encoding? encoding = null)
    {
        File.WriteAllText(_testFilePath, content, encoding ?? new UTF8Encoding(false));
    }

    private static (long byteOffset, int rowOffset) GetFirstCheckpoint(string filePath)
    {
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        return indexer.GetCheckPoint(0);
    }

    [Fact]
    public void Constructor_WithNullFilePath_ThrowsArgumentNullException()
    {
        // Arrange
        string? filePath = null;

        // Act
        var act = () => new ElementReader(filePath!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithWhiteSpacePath_ThrowsArgumentException()
    {
        // Arrange
        var filePath = "   ";

        // Act
        var act = () => new ElementReader(filePath);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReadElements_WithUtf8BomPrefixedFile_ReadsElementsFromPostBomCheckpoint()
    {
        // Arrange — the checkpoint comes from the BOM-aware RowIndexer: BOM (3) + root '[' (1) = 4.
        WriteTestContent("""[{"a":1},{"a":2}]""", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var (byteOffset, rowOffset) = GetFirstCheckpoint(_testFilePath);

        // Act
        using var reader = new ElementReader(_testFilePath);
        var elements = reader.ReadElements(byteOffset, rowOffset, 2);

        // Assert
        byteOffset.Should().Be(4L);
        rowOffset.Should().Be(0);
        elements.Should().HaveCount(2);
        Encoding.UTF8.GetString(elements[0].Span).Should().Be("""{"a":1}""");
        Encoding.UTF8.GetString(elements[1].Span).Should().Be("""{"a":2}""");
    }

    [Fact]
    public void Constructor_WithNonExistentFile_ThrowsInvalidOperationException()
    {
        // Arrange
        var filePath = "non_existent.json";

        // Act
        var act = () => new ElementReader(filePath);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Failed to open memory-mapped file*");
    }

    [Fact]
    public void ReadElements_MixedTypeArray_ReturnsCorrectElements()
    {
        // Arrange
        WriteTestContent("[1, \"hello\", null, true, {\"a\":1}, [2]]");
        var (byteOffset, skip) = GetFirstCheckpoint(_testFilePath);
        using var reader = new ElementReader(_testFilePath);

        // Act
        var result = reader.ReadElements(byteOffset, skip, 10);

        // Assert
        result.Should().HaveCount(6);
        Encoding.UTF8.GetString(result[0].Span).Should().Be("1");
        Encoding.UTF8.GetString(result[1].Span).Should().Be("\"hello\"");
        Encoding.UTF8.GetString(result[2].Span).Should().Be("null");
        Encoding.UTF8.GetString(result[3].Span).Should().Be("true");
        Encoding.UTF8.GetString(result[4].Span).Should().Be("{\"a\":1}");
        Encoding.UTF8.GetString(result[5].Span).Should().Be("[2]");
    }

    [Fact]
    public void ReadElements_SkipPastEnd_ReturnsEmptyList()
    {
        // Arrange
        WriteTestContent("[1, 2, 3]");
        var (byteOffset, skip) = GetFirstCheckpoint(_testFilePath);
        using var reader = new ElementReader(_testFilePath);

        // Act
        var result = reader.ReadElements(byteOffset, skip + 100, 10);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ReadElements_SingleObject_ReturnsObjectBytes()
    {
        // Arrange
        WriteTestContent("[{\"a\":1}]");
        var (byteOffset, skip) = GetFirstCheckpoint(_testFilePath);
        using var reader = new ElementReader(_testFilePath);

        // Act
        var result = reader.ReadElements(byteOffset, skip, 10);

        // Assert
        result.Should().HaveCount(1);
        Encoding.UTF8.GetString(result[0].Span).Should().Be("{\"a\":1}");
    }

    [Fact]
    public void ReadElements_MultipleElements_ReturnsCorrectCount()
    {
        // Arrange
        WriteTestContent("[1, 2, 3]");
        var (byteOffset, skip) = GetFirstCheckpoint(_testFilePath);
        using var reader = new ElementReader(_testFilePath);

        // Act
        var result = reader.ReadElements(byteOffset, skip, 10);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public void ReadElements_WithSkip_SkipsCorrectElements()
    {
        // Arrange
        WriteTestContent("[1, 2, 3]");
        var (byteOffset, skip) = GetFirstCheckpoint(_testFilePath);
        using var reader = new ElementReader(_testFilePath);

        // Act
        var result = reader.ReadElements(byteOffset, skip + 1, 2);

        // Assert
        result.Should().HaveCount(2);
        Encoding.UTF8.GetString(result[0].Span).Should().Be("2");
        Encoding.UTF8.GetString(result[1].Span).Should().Be("3");
    }

    [Fact]
    public void ReadElements_ElementSpansBufferBoundary_ReturnsCompleteBytes()
    {
        // Arrange — inner array with 300 000 numbers totals ~1.4 MB, forcing multiple
        // 1 MB buffer fills for a single element. Individual tokens stay small (no > 1 MB strings).
        // byteOffset = 1: root '[' is at offset 0; the inner array element starts at offset 1.
        const int elementCount = 300_000;
        var numbers = string.Join(",", Enumerable.Range(0, elementCount)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        WriteTestContent($"[[{numbers}]]");
        using var reader = new ElementReader(_testFilePath);

        // Act
        var result = reader.ReadElements(byteOffset: 1, elementsToSkip: 0, elementsToFetch: 1);

        // Assert
        result.Should().HaveCount(1);
        var json = Encoding.UTF8.GetString(result[0].Span);
        json.Should().StartWith("[0,1,");
        json.Should().EndWith($",{elementCount - 1}]");
        json.Length.Should().BeGreaterThan(1024 * 1024);
    }

    [Fact]
    public void ReadElements_FetchBeyondEnd_ReturnsAvailableElements()
    {
        // Arrange
        WriteTestContent("[1, 2]");
        var (byteOffset, skip) = GetFirstCheckpoint(_testFilePath);
        using var reader = new ElementReader(_testFilePath);

        // Act
        var result = reader.ReadElements(byteOffset, skip, 10);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void ReadElements_WithZeroFetchCount_ReturnsEmptyList()
    {
        // Arrange
        WriteTestContent("[1, 2, 3]");
        var (byteOffset, skip) = GetFirstCheckpoint(_testFilePath);
        using var reader = new ElementReader(_testFilePath);

        // Act
        var result = reader.ReadElements(byteOffset, skip, 0);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ReadElements_WithNegativeSkipCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        WriteTestContent("[1]");
        var (byteOffset, _) = GetFirstCheckpoint(_testFilePath);
        using var reader = new ElementReader(_testFilePath);

        // Act
        var act = () => reader.ReadElements(byteOffset, -1, 1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReadElements_WithNegativeFetchCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        WriteTestContent("[1]");
        var (byteOffset, _) = GetFirstCheckpoint(_testFilePath);
        using var reader = new ElementReader(_testFilePath);

        // Act
        var act = () => reader.ReadElements(byteOffset, 0, -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReadElements_EmptyArray_ReturnsEmptyList()
    {
        // Arrange — empty array has no checkpoints; byteOffset = -1 from GetCheckPoint.
        WriteTestContent("[]");
        using var reader = new ElementReader(_testFilePath);

        // Act
        var result = reader.ReadElements(-1, 0, 10);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ReadElements_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        WriteTestContent("[1]");
        var reader = new ElementReader(_testFilePath);
        reader.Dispose();

        // Act
        var act = () => reader.ReadElements(0, 0, 1);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }
}

using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Cli;

public sealed class ColumnNameResolverTests : IDisposable
{
    private readonly string _testDir;

    public ColumnNameResolverTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveColumnNames_WithCsvInput_ReturnsHeaderColumnNames()
    {
        // Arrange
        var inputFile = CreateTestFile("input.csv", "name,age\nAlice,30");

        // Act
        var names = ColumnNameResolver.ResolveColumnNames(DataFormat.Csv, inputFile, CancellationToken.None);

        // Assert
        names.Should().Equal(["name", "age"]);
    }

    [Fact]
    public void ResolveColumnNames_WithJsonLinesSpanningMultipleBatches_IncludesColumnsFromEveryBatch()
    {
        // Arrange — 1001 rows spans two batches (BatchSize = 1000); "b" only appears in the second.
        var lines = Enumerable.Range(0, 1000).Select(_ => """{"a":1}""").Append("""{"a":1,"b":2}""");
        var inputFile = CreateTestFile("input.jsonl", $"{string.Join("\n", lines)}\n");

        // Act
        var names = ColumnNameResolver.ResolveColumnNames(DataFormat.JsonLines, inputFile, CancellationToken.None);

        // Assert
        names.Should().Equal(["a", "b"]);
    }

    [Fact]
    public void ResolveColumnNames_WithJsonLinesColumnBeyondInitialScanCap_IncludesColumn()
    {
        // Arrange — "late" first appears at row 201, beyond the old 200-row initial-scan cap.
        var lines = Enumerable.Range(0, 200).Select(_ => """{"a":1}""").Append("""{"a":1,"late":2}""");
        var inputFile = CreateTestFile("input.jsonl", $"{string.Join("\n", lines)}\n");

        // Act
        var names = ColumnNameResolver.ResolveColumnNames(DataFormat.JsonLines, inputFile, CancellationToken.None);

        // Assert
        names.Should().Equal(["a", "late"]);
    }

    [Fact]
    public void ResolveColumnNames_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var inputFile = CreateTestFile("input.jsonl", """{"a":1}""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Action act = () => ColumnNameResolver.ResolveColumnNames(DataFormat.JsonLines, inputFile, cts.Token);

        // Assert
        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void ResolveColumnNames_WithUnsupportedFormat_ThrowsNotSupportedException()
    {
        // Arrange
        var inputFile = CreateTestFile("input.json", "[1,2,3]");

        // Act
        Action act = () => ColumnNameResolver.ResolveColumnNames(DataFormat.JsonArray, inputFile, CancellationToken.None);

        // Assert
        act.Should().Throw<NotSupportedException>().WithMessage("*JsonArray*");
    }

    private string CreateTestFile(string fileName, string content)
    {
        var filePath = Path.Combine(_testDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }
}

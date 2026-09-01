using AwesomeAssertions;
using Refedle.App;
using Refedle.Engine.Types;

namespace Refedle.Tests.App;

public sealed class FormatDetectorTests : IDisposable
{
    private readonly string _csvFilePath;
    private readonly string _jsonlFilePath;
    private readonly string _jsonArrayFilePath;
    private readonly string _unsupportedFilePath;
    private readonly string _emptyFilePath;
    private readonly string _nonExistentFilePath;
    private readonly string _jsonObjectFilePath;
    private readonly string _jsonObjectWithWhitespacePath;
    private readonly string _jsonArrayWithWhitespacePath;
    private readonly string _jsonUnknownRootPath;
    private readonly string _whitespaceOnlyJsonPath;
    private readonly string _jsonObjectWithBomPath;

    public FormatDetectorTests()
    {
        _csvFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        _jsonlFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
        _jsonArrayFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        _unsupportedFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".txt");
        _emptyFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        _nonExistentFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        _jsonObjectFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        _jsonObjectWithWhitespacePath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        _jsonArrayWithWhitespacePath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        _jsonUnknownRootPath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        _whitespaceOnlyJsonPath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        _jsonObjectWithBomPath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
    }

    public void Dispose()
    {
        foreach (var path in new[]
        {
            _csvFilePath, _jsonlFilePath, _jsonArrayFilePath, _unsupportedFilePath, _emptyFilePath,
            _jsonObjectFilePath, _jsonObjectWithWhitespacePath, _jsonArrayWithWhitespacePath,
            _jsonUnknownRootPath, _whitespaceOnlyJsonPath, _jsonObjectWithBomPath,
        })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DetectInputFile_WithNullOrEmptyPath_ReturnsFailure(string? path)
    {
        // Act
        var result = FormatDetector.DetectInputFile(path!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("path cannot be empty");
    }

    [Fact]
    public async Task DetectInputFile_WithCsvFile_ReturnsCsvFormat()
    {
        // Arrange
        await File.WriteAllTextAsync(_csvFilePath, "header\ndata");

        // Act
        var result = FormatDetector.DetectInputFile(_csvFilePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.Csv);
    }

    [Fact]
    public async Task DetectInputFile_WithJsonLinesFile_ReturnsJsonLinesFormat()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{}");

        // Act
        var result = FormatDetector.DetectInputFile(_jsonlFilePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonLines);
    }

    [Fact]
    public async Task DetectInputFile_WithJsonArrayFile_ReturnsJsonArrayFormat()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonArrayFilePath, "[1,2,3]");

        // Act
        var result = FormatDetector.DetectInputFile(_jsonArrayFilePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonArray);
    }

    [Fact]
    public async Task DetectInputFile_WithUnsupportedFormat_ReturnsFailure()
    {
        // Arrange
        await File.WriteAllTextAsync(_unsupportedFilePath, "{}");

        // Act
        var result = FormatDetector.DetectInputFile(_unsupportedFilePath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unsupported file format");
    }

    [Fact]
    public void DetectInputFile_WithNonExistentFile_ReturnsFailure()
    {
        // Act
        var result = FormatDetector.DetectInputFile(_nonExistentFilePath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not exist");
    }

    [Fact]
    public async Task DetectInputFile_WithEmptyFile_ReturnsFailure()
    {
        // Arrange
        await File.WriteAllBytesAsync(_emptyFilePath, []);

        // Act
        var result = FormatDetector.DetectInputFile(_emptyFilePath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is empty");
    }

    [Fact]
    public async Task DetectInputFile_WithJsonObjectFile_ReturnsJsonObject()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonObjectFilePath, "{\"id\":1}");

        // Act
        var result = FormatDetector.DetectInputFile(_jsonObjectFilePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonObject);
    }

    [Fact]
    public async Task DetectInputFile_WithJsonObjectFileAndLeadingWhitespace_ReturnsJsonObject()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonObjectWithWhitespacePath, "  \n{\"id\":1}");

        // Act
        var result = FormatDetector.DetectInputFile(_jsonObjectWithWhitespacePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonObject);
    }

    [Fact]
    public async Task DetectInputFile_WithJsonArrayFileAndLeadingWhitespace_ReturnsJsonArray()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonArrayWithWhitespacePath, "  \n[1,2,3]");

        // Act
        var result = FormatDetector.DetectInputFile(_jsonArrayWithWhitespacePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonArray);
    }

    [Fact]
    public async Task DetectInputFile_WithJsonFileWithUnknownRoot_ReturnsFailure()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonUnknownRootPath, "\"hello\"");

        // Act
        var result = FormatDetector.DetectInputFile(_jsonUnknownRootPath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unrecognized JSON root token");
    }

    [Fact]
    public async Task DetectInputFile_WithWhitespaceOnlyJsonFile_ReturnsFailure()
    {
        // Arrange
        await File.WriteAllTextAsync(_whitespaceOnlyJsonPath, "   \t\r\n  ");

        // Act
        var result = FormatDetector.DetectInputFile(_whitespaceOnlyJsonPath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("no valid JSON root token");
    }

    [Fact]
    public async Task DetectInputFile_WithJsonObjectFileAndBom_ReturnsJsonObject()
    {
        // Arrange — write UTF-8 BOM (0xEF 0xBB 0xBF) followed by a JSON object
        byte[] content = [0xEF, 0xBB, 0xBF, .. "{\"id\":1}"u8];
        await File.WriteAllBytesAsync(_jsonObjectWithBomPath, content);

        // Act
        var result = FormatDetector.DetectInputFile(_jsonObjectWithBomPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonObject);
    }
}

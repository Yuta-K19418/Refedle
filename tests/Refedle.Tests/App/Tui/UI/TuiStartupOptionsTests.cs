using AwesomeAssertions;
using Refedle.App.Tui.UI;
using Refedle.TestHelpers.IO;

namespace Refedle.Tests.App.Tui.UI;

public sealed class TuiStartupOptionsTests : IDisposable
{
    private readonly TempFile _tempFile = TempFile.CreateEmpty(".tmp");

    private readonly string _existingFile;

    private readonly string _missingFile;

    public TuiStartupOptionsTests()
    {
        _existingFile = _tempFile.Path;
        _missingFile = Path.Combine(Path.GetTempPath(), $"refedle-missing-{Guid.NewGuid():N}");
    }

    public void Dispose() => _tempFile.Dispose();

    [Fact]
    public void FindMissingFileError_WhenNoFilesReferenced_ReturnsNull()
    {
        // Arrange
        TuiStartupOptions options = new();

        // Act
        string? error = options.FindMissingFileError();

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void FindMissingFileError_WhenAllReferencedFilesExist_ReturnsNull()
    {
        // Arrange
        TuiStartupOptions options = new(InputFile: _existingFile, RecipeFile: _existingFile);

        // Act
        string? error = options.FindMissingFileError();

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void FindMissingFileError_WhenInputFileIsMissing_ReturnsInputFileError()
    {
        // Arrange
        TuiStartupOptions options = new(InputFile: _missingFile, RecipeFile: _existingFile);

        // Act
        string? error = options.FindMissingFileError();

        // Assert
        error.Should().Be($"Error: File not found: {_missingFile}");
    }

    [Fact]
    public void FindMissingFileError_WhenRecipeFileIsMissing_ReturnsRecipeFileError()
    {
        // Arrange
        TuiStartupOptions options = new(InputFile: _existingFile, RecipeFile: _missingFile);

        // Act
        string? error = options.FindMissingFileError();

        // Assert
        error.Should().Be($"Error: Recipe file not found: {_missingFile}");
    }

    [Fact]
    public void FindMissingFileError_WhenBothFilesAreMissing_ReportsInputFileFirst()
    {
        // Arrange
        TuiStartupOptions options = new(InputFile: _missingFile, RecipeFile: _missingFile);

        // Act
        string? error = options.FindMissingFileError();

        // Assert
        error.Should().Be($"Error: File not found: {_missingFile}");
    }
}

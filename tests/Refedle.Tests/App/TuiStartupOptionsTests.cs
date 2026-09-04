using AwesomeAssertions;
using Refedle.App;

namespace Refedle.Tests.App;

public sealed class TuiStartupOptionsTests : IDisposable
{
    private readonly string _existingFile;
    private readonly string _missingFile;

    public TuiStartupOptionsTests()
    {
        _existingFile = Path.GetTempFileName();
        _missingFile = Path.Combine(Path.GetTempPath(), $"refedle-missing-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (File.Exists(_existingFile))
        {
            File.Delete(_existingFile);
        }
    }

    [Fact]
    public void FindMissingFileError_WhenNoFilesReferenced_ReturnsNull()
    {
        // Arrange
        var options = new TuiStartupOptions();

        // Act
        var error = options.FindMissingFileError();

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void FindMissingFileError_WhenAllReferencedFilesExist_ReturnsNull()
    {
        // Arrange
        var options = new TuiStartupOptions(InputFile: _existingFile, RecipeFile: _existingFile);

        // Act
        var error = options.FindMissingFileError();

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void FindMissingFileError_WhenInputFileIsMissing_ReturnsInputFileError()
    {
        // Arrange
        var options = new TuiStartupOptions(InputFile: _missingFile, RecipeFile: _existingFile);

        // Act
        var error = options.FindMissingFileError();

        // Assert
        error.Should().Be($"Error: File not found: {_missingFile}");
    }

    [Fact]
    public void FindMissingFileError_WhenRecipeFileIsMissing_ReturnsRecipeFileError()
    {
        // Arrange
        var options = new TuiStartupOptions(InputFile: _existingFile, RecipeFile: _missingFile);

        // Act
        var error = options.FindMissingFileError();

        // Assert
        error.Should().Be($"Error: Recipe file not found: {_missingFile}");
    }

    [Fact]
    public void FindMissingFileError_WhenBothFilesAreMissing_ReportsInputFileFirst()
    {
        // Arrange
        var options = new TuiStartupOptions(InputFile: _missingFile, RecipeFile: _missingFile);

        // Act
        var error = options.FindMissingFileError();

        // Assert
        error.Should().Be($"Error: File not found: {_missingFile}");
    }
}

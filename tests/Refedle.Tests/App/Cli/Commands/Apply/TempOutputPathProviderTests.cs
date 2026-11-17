using AwesomeAssertions;
using Refedle.App.Cli.Commands.Apply;

namespace Refedle.Tests.App.Cli.Commands.Apply;

public sealed class TempOutputPathProviderTests
{
    [Fact]
    public void NewPath_WithOutputFile_ReturnsPathInSameDirectory()
    {
        // Arrange
        TempOutputPathProvider provider = new();
        string outputFile = Path.Combine("some", "dir", "output.csv");

        // Act
        string tempPath = provider.NewPath(outputFile);

        // Assert
        Path.GetDirectoryName(tempPath).Should().Be(Path.GetDirectoryName(outputFile));
    }

    [Fact]
    public void NewPath_CalledTwice_ReturnsDifferentPaths()
    {
        // Arrange
        TempOutputPathProvider provider = new();
        string outputFile = Path.Combine("some", "dir", "output.csv");

        // Act
        string first = provider.NewPath(outputFile);
        string second = provider.NewPath(outputFile);

        // Assert
        second.Should().NotBe(first);
    }
}

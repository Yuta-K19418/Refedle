using AwesomeAssertions;
using Refedle.App.Cli.Commands.Apply;

namespace Refedle.Tests.App.Cli.Commands.Apply;

public sealed class TempOutputPathProviderTests
{
    [Fact]
    public void NewPath_WithOutputFile_ReturnsPathInSameDirectory()
    {
        // Arrange
        var provider = new TempOutputPathProvider();
        var outputFile = Path.Combine("some", "dir", "output.csv");

        // Act
        var tempPath = provider.NewPath(outputFile);

        // Assert
        Path.GetDirectoryName(tempPath).Should().Be(Path.GetDirectoryName(outputFile));
    }

    [Fact]
    public void NewPath_CalledTwice_ReturnsDifferentPaths()
    {
        // Arrange
        var provider = new TempOutputPathProvider();
        var outputFile = Path.Combine("some", "dir", "output.csv");

        // Act
        var first = provider.NewPath(outputFile);
        var second = provider.NewPath(outputFile);

        // Assert
        second.Should().NotBe(first);
    }
}

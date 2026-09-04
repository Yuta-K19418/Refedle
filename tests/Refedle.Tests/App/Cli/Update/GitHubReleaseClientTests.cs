using AwesomeAssertions;
using Refedle.App.Cli.Update;

namespace Refedle.Tests.App.Cli.Update;

public sealed class GitHubReleaseClientTests
{
    [Theory]
    [InlineData("https://github.com/Yuta-K19418/Refedle/releases/tag/v0.3.0", "v0.3.0")]
    [InlineData("/Yuta-K19418/Refedle/releases/tag/v1.2.3", "v1.2.3")]
    [InlineData("https://github.com/Yuta-K19418/Refedle/releases/tag/0.10.0", "0.10.0")]
    public void TryExtractTag_WithTagInLocation_ReturnsTrueAndTag(string location, string expectedTag)
    {
        // Arrange

        // Act
        var extracted = GitHubReleaseClient.TryExtractTag(location, out var tag);

        // Assert
        extracted.Should().BeTrue();
        tag.Should().Be(expectedTag);
    }

    [Theory]
    [InlineData("https://github.com/Yuta-K19418/Refedle/releases")]
    [InlineData("https://github.com/Yuta-K19418/Refedle/releases/latest")]
    [InlineData("https://github.com/Yuta-K19418/Refedle/releases/tag/v0.3.0/files")]
    [InlineData("")]
    public void TryExtractTag_WithoutTagSegment_ReturnsFalseAndEmpty(string location)
    {
        // Arrange

        // Act
        var extracted = GitHubReleaseClient.TryExtractTag(location, out var tag);

        // Assert
        extracted.Should().BeFalse();
        tag.Should().BeEmpty();
    }
}
